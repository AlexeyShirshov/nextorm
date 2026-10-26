using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// The runtime <see cref="TvfEntityBuilder{T}"/> factories used by the SQL Server generation tests. The
/// metadata is constructor-only and independent of the argument values, so one factory can serve the
/// literal, captured, null and nested-expression argument shapes.
/// </summary>
internal static class T320SsTvf
{
    public static TvfEntityBuilder<ISimpleEntity> Echo(long id) => new("t320_echo");

    public static TvfEntityBuilder<ISimpleEntity> EchoNullable(long? id) => new("t320_echo_nullable");

    public static TvfEntityBuilder<ISimpleEntity> EchoPair(long id, int extra) => new("t320_echo_pair");

    public static TvfEntityBuilder<ISimpleEntity> JoinSource(long id) => new("t320_join_probe");

    public static TvfEntityBuilder<ISimpleEntity> InnerSource(long id) => new("t320_inner_probe");

    public static TvfEntityBuilder<ISimpleEntity> IdentityAlpha(string label) => new("t320_identity_alpha", "dbo");

    public static TvfEntityBuilder<ISimpleEntity> IdentityBeta(string label) => new("t320_identity_beta", "dbo");

    public static TvfEntityBuilder<ISimpleEntity> IdentityOtherSchema(string label) => new("t320_identity_alpha", "t320_other");

    public static TvfEntityBuilder<ISimpleEntity> IdentityVerbatim(string label) => new(
        "t320_identity_alpha",
        "dbo",
        null,
        null,
        new[] { 0 },
        TableFunctionSchema.None);

    public static TvfEntityBuilder<ISimpleEntity> EquivalentOne(long id) => new("t320_equivalent", "dbo");

    public static TvfEntityBuilder<ISimpleEntity> EquivalentTwo(long id) => new("t320_equivalent", "dbo");

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

    public static TvfEntityBuilder<ISimpleEntity> CustomEcho(long id) => new("t320_custom_echo", "dbo");

    public static TvfEntityBuilder<ISimpleEntity> CustomGenerateSeries(long start, long stop) => new("generate_series");
}

/// <summary>
/// #320 (T320) SQL Server SQL generation for the runtime TVF source (matrix row MX-SS). These tests never
/// open a database connection: the connection string is a placeholder, so they run on every build. The
/// live counterpart is <c>T320SqlServerTvfTests</c> in the integration project.
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
        using var ctx = SqlServerTestContext.Create();

        var prepared = Prepare(ctx, ctx.From(() => T320SsTvf.Echo(42)).Select(r => r.Id), false);
        var sql = Normalize(prepared.DbCommand.CommandText);
        var parameters = ParametersOf(prepared);
        output.WriteLine($"literal: sql=<<{sql}>>; parameters=[{string.Join(", ", parameters)}]");

        sql.Should().Be("select id\n from t320_echo(42) as [t1]", "a literal argument renders inline on the call");
        parameters.Should().BeEmpty("a literal argument is not parameterised");
    }

    [Fact]
    public void RuntimeTvf_C_CapturedArgument_ShouldBindAParameter()
    {
        using var ctx = SqlServerTestContext.Create();
        var id = 42L;

        var prepared = Prepare(ctx, ctx.From(() => T320SsTvf.Echo(id)).Select(r => r.Id), false);
        var sql = Normalize(prepared.DbCommand.CommandText);
        var parameters = ParametersOf(prepared);
        output.WriteLine($"capture: sql=<<{sql}>>; parameters=[{string.Join(", ", parameters)}]");

        sql.Should().Be("select id\n from t320_echo(@id) as [t1]", "a captured value is bound, never inlined");
        parameters.Should().Equal(new[] { "id=42" }, "the capture travels as one parameter with the captured value");
    }

    [Fact]
    public void RuntimeTvf_N_NullArgument_ShouldRenderNullWithoutAParameter()
    {
        using var ctx = SqlServerTestContext.Create();

        var prepared = Prepare(ctx, ctx.From(() => T320SsTvf.EchoNullable(null)).Select(r => r.Id), false);
        var sql = Normalize(prepared.DbCommand.CommandText);
        var parameters = ParametersOf(prepared);
        output.WriteLine($"null: sql=<<{sql}>>; parameters=[{string.Join(", ", parameters)}]");

        sql.Should().Be("select id\n from t320_echo_nullable(null) as [t1]", "a null argument stays the SQL NULL literal");
        parameters.Should().BeEmpty("a null literal is not replaced by a metadata-evaluation default");
    }

    [Fact]
    public void RuntimeTvf_X_NestedSqlFunctionArgument_ShouldKeepTheNestedCallAndTheParameter()
    {
        using var ctx = SqlServerTestContext.Create();
        var captured = 42L;

        var prepared = Prepare(
            ctx,
            ctx.From(() => T320SsTvf.EchoNullable(SqlFunctions.Sql.nullif(captured, 0L))).Select(r => r.Id),
            false);
        var sql = Normalize(prepared.DbCommand.CommandText);
        var parameters = ParametersOf(prepared);
        output.WriteLine($"nested: sql=<<{sql}>>; parameters=[{string.Join(", ", parameters)}]");

        sql.Should().Contain("from t320_echo_nullable(nullif(@captured, 0))", "the nested SQL function is the argument, not its value");
        sql.Should().NotContain("t320_echo_nullable(42)", "the nested expression is never evaluated into a constant");
        parameters.Should().Equal(new[] { "captured=42" }, "the capture inside the nested call is a bound parameter");
    }

    // ---- A: correlated APPLY ---------------------------------------------------------------------

    [Fact]
    public void RuntimeTvf_A_CrossApply_ShouldRenderCrossApplyWithTheOuterColumn()
    {
        using var ctx = SqlServerTestContext.Create();
        var extra = 7;

        var prepared = Prepare(
            ctx,
            ctx.From<ISimpleEntity>()
                .CrossApply(row => T320SsTvf.EchoPair(row.Id, extra))
                .Select(p => new { Left = p.Item1.Id, Right = p.Item2.Id }),
            false);
        var sql = Normalize(prepared.DbCommand.CommandText);
        var parameters = ParametersOf(prepared);
        output.WriteLine($"cross apply: sql=<<{sql}>>; parameters=[{string.Join(", ", parameters)}]");

        sql.Should().Contain("cross apply ", "SQL Server renders a correlated inner APPLY natively");
        sql.Should().Contain("t320_echo_pair", "the runtime factory supplies the applied source name");
        sql.Should().Contain("t1.id", "the correlated argument stays an outer column reference");
        sql.Should().Contain("@extra", "the captured argument is a bound parameter");
        sql.Should().NotContain("t320_echo_pair(7", "the outer column and the capture are never evaluated into the call text");
        parameters.Should().Equal("extra=7");
        sql.Should().Be(
            "select t1.id as [Left], t3.id as [Right]\n"
            + " from simple_entity as [t1]\n"
            + " cross apply (select t2.id\n"
            + " from t320_echo_pair(cast(t1.id as bigint), @extra) as [t2]) as [t3]",
            "the correlated inner APPLY keeps the outer column reference and binds the capture");
    }

    [Fact]
    public void RuntimeTvf_A_OuterApply_ShouldRenderOuterApplyWithTheOuterColumn()
    {
        using var ctx = SqlServerTestContext.Create();
        var extra = 7;

        var prepared = Prepare(
            ctx,
            ctx.From<ISimpleEntity>()
                .OuterApply(row => T320SsTvf.EchoPair(row.Id, extra))
                .Select(p => new { Left = p.Item1.Id, Right = p.Item2.Id }),
            false);
        var sql = Normalize(prepared.DbCommand.CommandText);
        var parameters = ParametersOf(prepared);
        output.WriteLine($"outer apply: sql=<<{sql}>>; parameters=[{string.Join(", ", parameters)}]");

        sql.Should().Contain("outer apply ", "SQL Server renders a correlated outer APPLY natively");
        sql.Should().Contain("t320_echo_pair");
        sql.Should().Contain("t1.id", "the correlated argument stays an outer column reference");
        sql.Should().Contain("@extra");
        parameters.Should().Equal("extra=7");
        sql.Should().Be(
            "select t1.id as [Left], t3.id as [Right]\n"
            + " from simple_entity as [t1]\n"
            + " outer apply (select t2.id\n"
            + " from t320_echo_pair(cast(t1.id as bigint), @extra) as [t2]) as [t3]",
            "the correlated outer APPLY keeps the outer column reference and binds the capture");
    }

    // ---- J: ordinary lambda Join and a mixed source ---------------------------------------------

    [Fact]
    public void RuntimeTvf_J_OrdinaryJoin_ShouldRenderTheJoinedTvfSource()
    {
        using var ctx = SqlServerTestContext.Create();
        var joinId = 3L;

        var prepared = Prepare(
            ctx,
            ctx.From<ISimpleEntity>()
                .Join(() => T320SsTvf.JoinSource(joinId), (left, right) => left.Id == right.Id)
                .Select(p => new { Left = p.Item1.Id, Right = p.Item2.Id }),
            false);
        var sql = Normalize(prepared.DbCommand.CommandText);
        var parameters = ParametersOf(prepared);
        output.WriteLine($"join: sql=<<{sql}>>; parameters=[{string.Join(", ", parameters)}]");

        sql.Should().Contain("join t320_join_probe(@joinId) as [t2]", "an ordinary lambda join renders the TVF as the right source");
        sql.Should().NotContain("cross apply", "an ordinary join is not an APPLY");
        parameters.Should().Equal("joinId=3");
    }

    [Fact]
    public void RuntimeTvf_J_MixedSource_ShouldJoinTheTvfToATable()
    {
        using var ctx = SqlServerTestContext.Create();
        var fromId = 5L;

        var prepared = Prepare(
            ctx,
            ctx.From(() => T320SsTvf.Echo(fromId))
                .Join(ctx.From<ISimpleEntity>(), (tvf, entity) => tvf.Id == entity.Id)
                .Select(p => new { Left = p.Item1.Id, Right = p.Item2.Id }),
            false);
        var sql = Normalize(prepared.DbCommand.CommandText);
        var parameters = ParametersOf(prepared);
        output.WriteLine($"mixed: sql=<<{sql}>>; parameters=[{string.Join(", ", parameters)}]");

        sql.Should().Contain("from t320_echo(@fromId) as [t1]", "the TVF is the FROM source");
        sql.Should().Contain("join simple_entity as [t2]", "the ordinary table composes as the joined source");
        parameters.Should().Equal("fromId=5");
    }

    // ---- S/U: type-changing projection, clone/upcast/reuse ---------------------------------------

    [Fact]
    public void RuntimeTvf_S_TypeChangingSelect_ShouldKeepTheSourceAndItsArgument()
    {
        using var ctx = SqlServerTestContext.Create();
        var id = 42L;

        // The composed projection changes the builder row type while the factory row type stays
        // ISimpleEntity: the source and its argument must survive that difference.
        var source = ctx.From(() => T320SsTvf.Echo(id).As(r => new { r.Id }));
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
        using var ctx = SqlServerTestContext.Create();
        var id = 7L;

        // Upcast: the descriptor is usable through the EntityBuilder<T> static type.
        TvfEntityBuilder<ISimpleEntity> descriptor = T320SsTvf.Echo(id);
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
        var first = Prepare(ctx, ctx.From(() => T320SsTvf.Echo(id)).Select(r => r.Id), false);
        var second = Prepare(ctx, ctx.From(() => T320SsTvf.Echo(id)).Select(r => r.Id), false);
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
        using var ctx = SqlServerTestContext.Create();

        // Caller-declared placement None means "the function supplies its own result shape": no leading
        // column list and no alias column list, just the call and its alias.
        var prepared = Prepare(ctx, ctx.From(() => T320SsTvf.Echo(42)).Select(r => r.Id), false);
        var sql = Normalize(prepared.DbCommand.CommandText);
        output.WriteLine($"placement none: sql=<<{sql}>>");

        sql.Should().Be("select id\n from t320_echo(42) as [t1]");
        sql.Should().NotContain(" with (", "None appends no WITH clause");
    }

    [Fact]
    public void RuntimeTvf_R_LeadingArgument_ShouldBeANamedCapabilityRefusal()
    {
        using var ctx = SqlServerTestContext.Create();

        // Matrix MX-SS is T/G/G: SQL Server expresses neither a leading column-definition argument nor an
        // alias column list, so both placements are named capability refusals, not silently dropped.
        Action act = () => Prepare(ctx, ctx.From(() => T320SsTvf.LeadingResultSchema(42)).Select(r => r.Id), false);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*'t320_leading'*'LeadingArgument' form*not supported by this provider*");
    }

    [Fact]
    public void RuntimeTvf_R_AliasColumnList_ShouldBeANamedCapabilityRefusal()
    {
        using var ctx = SqlServerTestContext.Create();

        Action act = () => Prepare(ctx, ctx.From(() => T320SsTvf.AliasResultSchema(42)).Select(r => r.Id), false);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*'t320_alias'*'AliasColumnList' form*not supported by this provider*");
    }

    // ---- F: custom TVF versus built-in classification -------------------------------------------

    [Fact]
    public void RuntimeTvf_F_CustomFunction_ShouldRender()
    {
        using var ctx = SqlServerTestContext.Create();
        var id = 11L;

        var prepared = Prepare(ctx, ctx.From(() => T320SsTvf.CustomEcho(id)).Select(r => r.Id), false);
        var sql = Normalize(prepared.DbCommand.CommandText);
        output.WriteLine($"custom: sql=<<{sql}>>; parameters=[{string.Join(", ", ParametersOf(prepared))}]");

        sql.Should().Be("select id\n from dbo.t320_custom_echo(@id) as [t1]", "a custom runtime TVF renders verbatim with its schema");
        ParametersOf(prepared).Should().Equal("id=11");
    }

    [Fact]
    public void RuntimeTvf_F_BuiltInThroughRuntimePath_ShouldBeARuntimeFactory()
    {
        using var ctx = SqlServerTestContext.Create();
        var csv = "a,b,c";
        var separator = ",";

        // Built-ins were migrated to the same runtime descriptor surface: string_split declares
        // TvfEntityBuilder<T>, so the runtime FROM route accepts it. The built-in support gate is a
        // separate classification (covered by the next fact), not a factory-contract refusal.
        var builtIn = typeof(SqlServerFunctions).GetMethod(
            nameof(SqlServerFunctions.string_split),
            [typeof(string), typeof(string)])!;
        var returnsTvfDescriptor = builtIn.ReturnType.IsGenericType
            && builtIn.ReturnType.GetGenericTypeDefinition() == typeof(TvfEntityBuilder<>);
        output.WriteLine(
            $"built-in {builtIn.Name} returns {builtIn.ReturnType}; is a runtime factory: {returnsTvfDescriptor}");

        returnsTvfDescriptor.Should().BeTrue(
            "a built-in table function declares the same runtime descriptor as a custom factory");
        builtIn.ReturnType.GetGenericTypeDefinition().Should().Be(typeof(TvfEntityBuilder<>));

        var prepared = Prepare(
            ctx,
            ctx.From(() => SqlFunctions.SqlServer.string_split(csv, separator)).Select(r => new { r.Value }),
            false);
        var sql = Normalize(prepared.DbCommand.CommandText);
        output.WriteLine($"built-in: sql=<<{sql}>>; parameters=[{string.Join(", ", ParametersOf(prepared))}]");

        sql.Should().Be("select value\n from string_split(@csv, @separator) as [t1]");
        ParametersOf(prepared).Should().Equal("csv=a,b,c", "separator=,");
    }

    [Fact]
    public void RuntimeTvf_F_BuiltInAndCustom_ShouldKeepTheSupportGateDistinct()
    {
        using var ctx = SqlServerTestContext.Create();

        // A built-in name SQL Server does not support is gated by the built-in classification.
        Action builtIn = () => Prepare(
            ctx,
            ctx.From(() => SqlFunctions.Postgres.generate_series(1, 3)).Select(r => r.Value),
            false);
        builtIn.Should().Throw<NotSupportedException>()
            .WithMessage("*'generate_series'*not supported by this provider*");

        // The same SQL name through a custom runtime factory is emitted verbatim: a runtime descriptor is
        // a user-defined function, so the built-in capability gate must not apply to it.
        var custom = Prepare(ctx, ctx.From(() => T320SsTvf.CustomGenerateSeries(1, 3)).Select(r => r.Id), false);
        var sql = Normalize(custom.DbCommand.CommandText);
        output.WriteLine($"custom generate_series: sql=<<{sql}>>");

        sql.Should().Contain("from generate_series(1, 3) as [t1]", "a custom TVF is not gated by the built-in support list");
    }

    // ---- H/I: cache-hit refresh and SQL identity -------------------------------------------------

    [Fact]
    public void RuntimeTvf_H_CacheHit_ShouldRefreshTheCapturedArgument()
    {
        using var ctx = SqlServerTestContext.Create();
        ctx.PurgeQueryCache();
        var id = 1L;

        QueryCommand<int> Build() => ctx.From(() => T320SsTvf.Echo(id)).Select(r => r.Id);

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
        using var ctx = SqlServerTestContext.Create();
        ctx.PurgeQueryCache();

        var before = QueryPlanStore.Values.Count();

        var alpha = Prepare(ctx, ctx.From(() => T320SsTvf.IdentityAlpha("raw")).Select(r => r.Id), true);
        var afterAlpha = QueryPlanStore.Values.Count();

        var beta = Prepare(ctx, ctx.From(() => T320SsTvf.IdentityBeta("raw")).Select(r => r.Id), true);
        var afterBeta = QueryPlanStore.Values.Count();

        var schema = Prepare(ctx, ctx.From(() => T320SsTvf.IdentityOtherSchema("raw")).Select(r => r.Id), true);
        var verbatim = Prepare(ctx, ctx.From(() => T320SsTvf.IdentityVerbatim("raw")).Select(r => r.Id), true);
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
        statements[0].Should().Contain("dbo.t320_identity_alpha");
        statements[1].Should().Contain("dbo.t320_identity_beta");
        statements[2].Should().Contain("t320_other.t320_identity_alpha");
        statements[3].Should().Contain("(raw)", "a verbatim argument is emitted as SQL, not bound");
        ReferenceEquals(alpha, beta).Should().BeFalse();
    }

    [Fact]
    public void RuntimeTvf_I_EquivalentSqlIdentities_ShouldShareOnePlan()
    {
        using var ctx = SqlServerTestContext.Create();
        ctx.PurgeQueryCache();
        var id = 5L;

        var before = QueryPlanStore.Values.Count();
        var one = Prepare(ctx, ctx.From(() => T320SsTvf.EquivalentOne(id)).Select(r => r.Id), true);
        var afterOne = QueryPlanStore.Values.Count();
        var two = Prepare(ctx, ctx.From(() => T320SsTvf.EquivalentTwo(id)).Select(r => r.Id), true);
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
