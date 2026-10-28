using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>
/// #320 (T320) PostgreSQL SQL generation for the runtime TVF source under a correlated APPLY. These
/// tests never open a database connection: the connection string is a placeholder and only the
/// rendered statement and its bound parameters are inspected.
/// </summary>
internal static class T320PgTvf
{
    public static TvfEntityBuilder<ISimpleEntity> Echo(long id) => new("t320_echo");

    public static TvfEntityBuilder<ISimpleEntity> EchoNullable(long? id) => new("t320_echo_nullable");

    public static TvfEntityBuilder<ISimpleEntity> EchoPair(long id, int extra) => new("t320_echo_pair");

    public static TvfEntityBuilder<ISimpleEntity> JoinSource(long id) => new("t320_join_probe");

    public static TvfEntityBuilder<ISimpleEntity> LeadingResultSchema(long id) => new(
        "t320_leading",
        null,
        null,
        null,
        Array.Empty<int>(),
        TableFunctionSchema.LeadingArgument);

    /// <summary>
    /// A custom declaration whose SQL name is the name of a PostgreSQL built-in table function: the two
    /// carry the same rendered call, so any difference between them is the render path, not the metadata.
    /// </summary>
    public static TvfEntityBuilder<ISimpleEntity> GenerateSeries(long start, long stop) => new("generate_series");

    public static TvfEntityBuilder<ISimpleEntity> IdentityAlpha(string label) => new("identity_alpha", "dbo");

    public static TvfEntityBuilder<ISimpleEntity> IdentityBeta(string label) => new("identity_beta", "dbo");

    public static TvfEntityBuilder<ISimpleEntity> IdentityOtherSchema(string label) => new("identity_alpha", "identity_other");

    public static TvfEntityBuilder<ISimpleEntity> EquivalentOne(int id) => new("equivalent_tvf", "dbo");

    public static TvfEntityBuilder<ISimpleEntity> EquivalentTwo(int id) => new("equivalent_tvf", "dbo");
}

/// <summary>Row shape of the <c>jsonb_to_record</c> alias-column-list cell (MX-PG R).</summary>
internal interface IT320PgRecordRow
{
    [Column("id")]
    int Id { get; set; }

    [Column("label")]
    string? Label { get; set; }
}

[Trait("Category", "T320")]
public class T320RuntimeTvfSqlTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> command, bool storeInCache)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(command, false, storeInCache, CancellationToken.None);

    private static List<string> ParametersOf<T>(DbPreparedQueryCommand<T> prepared)
        => [.. prepared.DbCommandParams.Cast<DbParameter>().Select(p => $"{p.ParameterName}={p.Value ?? "null"}")];

    [Fact]
    public void CorrelatedRuntimeTvfApply_ShouldRenderLateralWithTheOuterColumnAndAParameter()
    {
        using var ctx = PostgresTestContext.Create();
        var extra = 7;

        var prepared = Prepare(
            ctx,
            ctx.From<ISimpleEntity>()
                .CrossApply(row => T320PgTvf.EchoPair(row.Id, extra))
                .Select(p => new { Left = p.Item1.Id, Right = p.Item2.Id }),
            storeInCache: false);

        var sql = Normalize(prepared.DbCommand.CommandText);

        sql.Should().Contain("cross join lateral", "PostgreSQL renders the correlated APPLY as a lateral join");
        sql.Should().Contain("t320_echo_pair", "the runtime factory supplies the applied source name");
        sql.Should().Contain("t1.id", "the correlated argument stays an outer column reference");
        sql.Should().Contain("@extra", "a captured argument is a bound parameter");
        sql.Should().NotContain("t320_echo_pair(7", "the outer column must never be evaluated into a constant");

        ParametersOf(prepared).Should().Equal("extra=7");
    }

    [Fact]
    public void CorrelatedRuntimeTvfApply_CacheHit_ShouldRefreshTheCaptureAndKeepTheOuterColumn()
    {
        using var ctx = PostgresTestContext.Create();
        ctx.PurgeQueryCache();
        var extra = 7;

        QueryCommand<int> Build() => ctx.From<ISimpleEntity>()
            .CrossApply(row => T320PgTvf.EchoPair(row.Id, extra))
            .Select(p => p.Item1.Id);

        var first = Prepare(ctx, Build(), storeInCache: true);
        var firstSql = Normalize(first.DbCommand.CommandText);
        ParametersOf(first).Should().Equal("extra=7");
        firstSql.Should().Contain("t1.id");

        extra = 8;
        var second = Prepare(ctx, Build(), storeInCache: true);

        ReferenceEquals(first, second).Should().BeTrue(
            "the same correlated shape must reuse the cached plan so the refresh path is exercised");
        Normalize(second.DbCommand.CommandText).Should().Be(firstSql, "a capture change must not re-render the statement");
        ParametersOf(second).Should().Equal(new[] { "extra=8" }, "the cache hit must bind the new capture");
        Normalize(second.DbCommand.CommandText).Should().Contain(
            "t1.id",
            "the correlated column stays a column reference, never an evaluated value");
    }

    // ---- C/N/X: the argument shapes ---------------------------------------------------------------

    [Fact]
    public void RuntimeTvf_C_CapturedArgument_ShouldBindAParameter()
    {
        using var ctx = PostgresTestContext.Create();
        var id = 42L;

        var prepared = Prepare(ctx, ctx.From(() => T320PgTvf.Echo(id)).Select(r => r.Id), false);
        var sql = Normalize(prepared.DbCommand.CommandText);
        var parameters = ParametersOf(prepared);

        sql.Should().Be("select id\n from t320_echo(@id) as \"t1\"", "a captured value is bound, never inlined");
        parameters.Should().Equal(new[] { "id=42" }, "the capture travels as one parameter with the captured value");
        sql.Should().NotContain("42", "a captured value must not be inlined into the statement text");
    }

    [Fact]
    public void RuntimeTvf_N_NullArgument_ShouldRenderNullWithoutAParameter()
    {
        using var ctx = PostgresTestContext.Create();

        var prepared = Prepare(ctx, ctx.From(() => T320PgTvf.EchoNullable(null)).Select(r => r.Id), false);
        var sql = Normalize(prepared.DbCommand.CommandText);
        var parameters = ParametersOf(prepared);

        sql.Should().Be(
            "select id\n from t320_echo_nullable(null) as \"t1\"",
            "a null argument stays the SQL NULL literal, never a metadata-evaluation default");
        parameters.Should().BeEmpty("a null literal is not parameterised");
    }

    [Fact]
    public void RuntimeTvf_X_NestedSqlFunctionArgument_ShouldKeepTheNestedCallAndTheParameter()
    {
        using var ctx = PostgresTestContext.Create();
        var captured = 42L;

        var prepared = Prepare(
            ctx,
            ctx.From(() => T320PgTvf.EchoNullable(SqlFunctions.Sql.nullif(captured, 0L))).Select(r => r.Id),
            false);
        var sql = Normalize(prepared.DbCommand.CommandText);
        var parameters = ParametersOf(prepared);

        sql.Should().Contain(
            "from t320_echo_nullable(nullif(@captured, 0))",
            "the nested SQL function is the argument, not its evaluated value");
        sql.Should().NotContain("t320_echo_nullable(42)", "the nested expression is never folded into a constant");
        parameters.Should().Equal(new[] { "captured=42" }, "the capture inside the nested call is a bound parameter");
    }

    // ---- J: ordinary lambda Join and a mixed source ---------------------------------------------

    [Fact]
    public void RuntimeTvf_J_OrdinaryJoinAndMixedSource_ShouldRenderTheTvfSource()
    {
        using var ctx = PostgresTestContext.Create();
        var joinId = 3L;

        var joined = Prepare(
            ctx,
            ctx.From<ISimpleEntity>()
                .Join(() => T320PgTvf.JoinSource(joinId), (left, right) => left.Id == right.Id)
                .Select(p => new { Left = p.Item1.Id, Right = p.Item2.Id }),
            false);
        var joinedSql = Normalize(joined.DbCommand.CommandText);
        joinedSql.Should().Contain(
            "join t320_join_probe(@joinId) as \"t2\"",
            "an ordinary lambda join renders the TVF as the right source");
        joinedSql.Should().NotContain("lateral", "an ordinary join is not an APPLY");
        ParametersOf(joined).Should().Equal(new[] { "joinId=3" });

        var fromId = 5L;
        var mixed = Prepare(
            ctx,
            ctx.From(() => T320PgTvf.Echo(fromId))
                .Join(ctx.From<ISimpleEntity>(), (tvf, entity) => tvf.Id == entity.Id)
                .Select(p => new { Left = p.Item1.Id, Right = p.Item2.Id }),
            false);
        var mixedSql = Normalize(mixed.DbCommand.CommandText);
        mixedSql.Should().Contain("from t320_echo(@fromId) as \"t1\"", "the TVF is the FROM source");
        mixedSql.Should().Contain("join simple_entity as \"t2\"", "the ordinary table composes as the joined source");
        ParametersOf(mixed).Should().Equal(new[] { "fromId=5" });
    }

    // ---- R: result placement ---------------------------------------------------------------------

    [Fact]
    public void RuntimeTvf_R_ResultPlacements_ShouldRenderNoneAndAliasColumnsAndRefuseLeading()
    {
        using var ctx = PostgresTestContext.Create();

        // None: the function supplies its own result shape, so the call is bare.
        var none = Prepare(ctx, ctx.From(() => T320PgTvf.Echo(42)).Select(r => r.Id), false);
        var noneSql = Normalize(none.DbCommand.CommandText);
        noneSql.Should().Be("select id\n from t320_echo(42) as \"t1\"", "None appends no column-definition list");

        // AliasColumnList: PostgreSQL's record functions carry the row-derived schema as the alias
        // column-definition list (jsonb_to_record(...) as "t1"(id integer, label text)).
        var json = "{\"id\":1,\"label\":\"a\"}";
        var record = Prepare(
            ctx,
            ctx.From(() => SqlFunctions.Postgres.jsonb_to_record<IT320PgRecordRow>(json)).Select(r => new { r.Id, r.Label }),
            false);
        var recordSql = Normalize(record.DbCommand.CommandText);
        recordSql.Should().Contain(
            "from jsonb_to_record(@json) as \"t1\"(id integer, label text)",
            "the alias column-definition list is rendered from the row type's mapped properties");
        ParametersOf(record).Should().Equal(new[] { "json={\"id\":1,\"label\":\"a\"}" });

        // LeadingArgument: PostgreSQL has no leading column-definition argument, so the form is a named
        // capability refusal, not silently dropped.
        Action leading = () => Prepare(ctx, ctx.From(() => T320PgTvf.LeadingResultSchema(42)).Select(r => r.Id), false);
        leading.Should().Throw<NotSupportedException>()
            .WithMessage("*'t320_leading'*'LeadingArgument' form*not supported by this provider*");
    }

    // ---- L/S/U/F/I: the remaining SQL-generation cells of the PostgreSQL matrix -------------------

    /// <summary>
    /// L: a literal argument is an inline SQL literal on PostgreSQL, never a bound parameter — the cell
    /// that keeps AC02 open until it has a runnable fact.
    /// </summary>
    [Fact]
    public void RuntimeTvf_L_LiteralArgument_ShouldRenderInlineWithoutAParameter()
    {
        using var ctx = PostgresTestContext.Create();

        var prepared = Prepare(ctx, ctx.From(() => T320PgTvf.Echo(42)).Select(r => r.Id), false);
        var sql = Normalize(prepared.DbCommand.CommandText);
        var parameters = ParametersOf(prepared);

        sql.Should().Be(
            "select id\n from t320_echo(42) as \"t1\"",
            "a literal argument renders as an inline SQL literal in the call itself");
        parameters.Should().BeEmpty("a literal argument is not parameterised");
        sql.Should().NotContain("@", "a literal must never be turned into a bound parameter");
    }

    /// <summary>
    /// S: a type-changing <c>Select</c> over the TVF source keeps the source, its parameterised argument
    /// and the projection on one statement.
    /// </summary>
    [Fact]
    public void RuntimeTvf_S_TypeChangingSelect_ShouldKeepTheSourceAndItsArgument()
    {
        using var ctx = PostgresTestContext.Create();
        var id = 42L;

        // The projection changes the builder row type from ISimpleEntity (the factory row type) to an
        // anonymous shape, so the source and its argument must be tracked separately from the result type.
        var prepared = Prepare(
            ctx,
            ctx.From(() => T320PgTvf.Echo(id)).Select(r => new { r.Id, Doubled = r.Id * 2 }),
            false);
        var sql = Normalize(prepared.DbCommand.CommandText);

        sql.Should().Contain("from t320_echo(@id) as \"t1\"", "the factory call and its argument survive the projection");
        sql.Should().Contain("(id * 2) as \"Doubled\"", "the type-changing projection stays on the same statement");
        ParametersOf(prepared).Should().Equal(new[] { "id=42" }, "the argument stays a bound capture after the projection");
    }

    /// <summary>
    /// U: a clone and an upcast of a builder carrying the TVF source keep the source and its argument, and
    /// two consumers of the same descriptor shape render the same argument.
    /// </summary>
    [Fact]
    public void RuntimeTvf_U_CloneUpcastAndReuse_ShouldKeepTheSourceAndItsArgument()
    {
        using var ctx = PostgresTestContext.Create();
        var id = 7L;

        var descriptor = T320PgTvf.Echo(id);
        EntityBuilder<ISimpleEntity> upcast = descriptor;
        var clone = upcast.Clone();

        clone.Should().BeOfType<TvfEntityBuilder<ISimpleEntity>>("the descriptor clone keeps the runtime type");
        clone.Should().NotBeSameAs(upcast);
        ((TvfEntityBuilder<ISimpleEntity>)clone).Name.Should().Be("t320_echo");

        // The supported copy path for SQL is the lowered builder: a *captured* descriptor loses the factory
        // call provenance and is rejected by name (SourceExpressionLowerer.LowerTvfSource), so the SQL copy
        // path is the clone of the already-lowered builder.
        var lowered = ctx.From(() => T320PgTvf.Echo(id));
        var loweredClone = (EntityBuilder<ISimpleEntity>)lowered.Clone();
        loweredClone.SourceFrom.Should().NotBeNull("the clone carries the lowered table-function source");

        // The upcast path is the lowered builder consumed through its base static type.
        EntityBuilder<ISimpleEntity> upcastLowered = ctx.From(() => T320PgTvf.Echo(id));

        var fromClone = Prepare(ctx, loweredClone.Select(r => r.Id), false);
        var fromUpcast = Prepare(ctx, upcastLowered.Select(r => r.Id), false);

        var cloneSql = Normalize(fromClone.DbCommand.CommandText);
        cloneSql.Should().Be("select id\n from t320_echo(@id) as \"t1\"", "a clone keeps the source and its argument slot");
        ParametersOf(fromClone).Should().Equal(new[] { "id=7" });
        Normalize(fromUpcast.DbCommand.CommandText).Should().Be(cloneSql, "an upcast renders the same statement as the clone");
        ParametersOf(fromUpcast).Should().Equal(ParametersOf(fromClone));

        // Reuse: two independent consumers of the same descriptor shape render the same TVF argument.
        var first = Prepare(ctx, ctx.From(() => T320PgTvf.Echo(id)).Select(r => r.Id), false);
        var second = Prepare(ctx, ctx.From(() => T320PgTvf.Echo(id)).Select(r => r.Id), false);
        Normalize(second.DbCommand.CommandText).Should().Be(Normalize(first.DbCommand.CommandText));
        ParametersOf(second).Should().Equal(ParametersOf(first));
    }

    /// <summary>
    /// F: a PostgreSQL built-in declaration and a custom factory with the same SQL name stay distinct in the
    /// render path (separate plan entries, built-in support check and literal policy); a built-in declared
    /// for another provider is admitted when the target dialect advertises the same name, and otherwise
    /// stays a named capability refusal.
    /// </summary>
    [Fact]
    public void RuntimeTvf_F_BuiltInGenerateSeriesVersusCustomFactory_ShouldStayDistinct()
    {
        using var ctx = PostgresTestContext.Create();
        ctx.PurgeQueryCache();

        var builtIn = Prepare(ctx, ctx.From(() => SqlFunctions.Postgres.generate_series(1, 3)).Select(r => r.Value), true);
        var builtInSql = Normalize(builtIn.DbCommand.CommandText);
        builtInSql.Should().Contain("from generate_series(1, 3)", "the built-in declaration renders its native call");
        var plansAfterBuiltIn = QueryPlanStore.Values.Count();
        plansAfterBuiltIn.Should().Be(1, "the built-in declaration stores one plan entry");

        // The custom factory carries the same name, schema and arguments, so its SQL is the same call, but
        // the built-in classification is part of the plan identity: the two must not share a plan entry.
        var custom = Prepare(ctx, ctx.From(() => T320PgTvf.GenerateSeries(1, 3)).Select(r => r.Id), true);
        var customSql = Normalize(custom.DbCommand.CommandText);
        customSql.Should().Contain("from generate_series(1, 3)", "a custom runtime factory is rendered verbatim");
        QueryPlanStore.Values.Count().Should().Be(
            plansAfterBuiltIn + 1,
            "the built-in verdict is part of the SQL identity, so a custom factory with the same name keeps its own plan");
        ReferenceEquals(builtIn, custom).Should().BeFalse("the two take different render paths");

        // The built-in gate is keyed by name against the *target* dialect, not the declaring class. Since
        // T347/#281 PostgreSQL advertises json_each as a set-returning function, so a SQLite-declared
        // builder carrying that name is admitted and rendered in the PostgreSQL spelling (the non-b
        // readers cast the text payload to json).
        var jsonEach = Prepare(ctx, ctx.From(() => SqlFunctions.Sqlite.json_each("[]")).Select(r => r.Value), false);
        var jsonEachSql = Normalize(jsonEach.DbCommand.CommandText);
        jsonEachSql.Should().Be(
            "select value\n from json_each(cast(E'[]' as json)) as \"t1\"",
            "a name the target dialect advertises is admitted whatever provider declared the member (T347/#281)");

        // The sibling name stays a named capability refusal: PostgreSQL has no json_tree, only SQLite does,
        // so the same foreign declaration is still rejected by name rather than silently rendered.
        Action builtInGate = () => Prepare(ctx, ctx.From(() => SqlFunctions.Sqlite.json_tree("[]")).Select(r => r.Value), false);
        builtInGate.Should().Throw<NotSupportedException>()
            .WithMessage("*'json_tree'*not supported by this provider*");
    }

    /// <summary>
    /// I: the SQL identity separates name, schema and argument value at SQL-generation level, and two
    /// equivalent factory shapes render one statement with one parameter list.
    /// </summary>
    [Fact]
    public void RuntimeTvf_I_SqlIdentities_ShouldRenderDistinctAndEquivalentStatements()
    {
        using var ctx = PostgresTestContext.Create();
        ctx.PurgeQueryCache();

        var alpha = Normalize(Prepare(ctx, ctx.From(() => T320PgTvf.IdentityAlpha("raw")).Select(r => r.Id), false).DbCommand.CommandText);
        var beta = Normalize(Prepare(ctx, ctx.From(() => T320PgTvf.IdentityBeta("raw")).Select(r => r.Id), false).DbCommand.CommandText);
        var schema = Normalize(Prepare(ctx, ctx.From(() => T320PgTvf.IdentityOtherSchema("raw")).Select(r => r.Id), false).DbCommand.CommandText);
        var argument = Normalize(Prepare(ctx, ctx.From(() => T320PgTvf.IdentityAlpha("other")).Select(r => r.Id), false).DbCommand.CommandText);

        new[] { alpha, beta, schema, argument }.Should().OnlyHaveUniqueItems(
            "a different name, schema or argument value renders a different statement");

        alpha.Should().Be("select id\n from dbo.identity_alpha(E'raw') as \"t1\"");
        beta.Should().Contain("dbo.identity_beta");
        schema.Should().Contain("identity_other.identity_alpha");
        argument.Should().Contain("E'other'");

        // Equivalent: two factory shapes with identical metadata and an identical capture render one
        // statement and one parameter list — at the SQL level, not only in the plan-key comparer.
        var id = 5;
        var one = Prepare(ctx, ctx.From(() => T320PgTvf.EquivalentOne(id)).Select(r => r.Id), true);
        var two = Prepare(ctx, ctx.From(() => T320PgTvf.EquivalentTwo(id)).Select(r => r.Id), true);
        Normalize(two.DbCommand.CommandText).Should().Be(
            Normalize(one.DbCommand.CommandText),
            "equivalent runtime metadata and argument shapes render the same statement");
        ParametersOf(two).Should().Equal(ParametersOf(one));
        ParametersOf(two).Should().Equal(new[] { "id=5" });
    }
}
