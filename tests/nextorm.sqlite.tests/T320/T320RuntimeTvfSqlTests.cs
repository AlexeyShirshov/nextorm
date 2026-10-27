using System.Data.Common;
using System.Runtime.CompilerServices;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// Two runtime <see cref="TvfEntityBuilder{T}"/> factory declarations used as a pair: the two carry
/// identical metadata, so any rendering difference between them is a lowering defect rather than a
/// metadata difference.
/// </summary>
internal static class T320ProbeTvf
{
    public static TvfEntityBuilder<ISimpleEntity> LegacyProbe(int id) => new("probe_tvf");

    public static TvfEntityBuilder<ISimpleEntity> ProbeTvf(int id) => new("probe_tvf");

    public static TvfEntityBuilder<ISimpleEntity> LegacyProbeText(string? text) => new("probe_tvf_text");

    public static TvfEntityBuilder<ISimpleEntity> ProbeTvfText(string? text) => new("probe_tvf_text");

    public static TvfEntityBuilder<ISimpleEntity> JoinSource(int id) => new("join_probe");

    public static TvfEntityBuilder<ISimpleEntity> InnerSource(int id) => new("inner_probe");

    public static TvfEntityBuilder<ISimpleEntity> IdentityAlpha(string label) => new("identity_alpha", "dbo");

    public static TvfEntityBuilder<ISimpleEntity> IdentityBeta(string label) => new("identity_beta", "dbo");

    public static TvfEntityBuilder<ISimpleEntity> IdentityOtherSchema(string label) => new("identity_alpha", "identity_other");

    public static TvfEntityBuilder<ISimpleEntity> IdentityVerbatim(string label) => new(
        "identity_alpha",
        "dbo",
        null,
        null,
        new[] { 0 },
        TableFunctionSchema.None);

    public static TvfEntityBuilder<ISimpleEntity> EquivalentOne(int id) => new("equivalent_tvf", "dbo");

    public static TvfEntityBuilder<ISimpleEntity> EquivalentTwo(int id) => new("equivalent_tvf", "dbo");

    public static TvfEntityBuilder<ISimpleEntity> LeadingResultSchema(int id) => new(
        "t320_leading",
        null,
        null,
        null,
        Array.Empty<int>(),
        TableFunctionSchema.LeadingArgument);

    public static TvfEntityBuilder<ISimpleEntity> AliasResultSchema(int id) => new(
        "t320_alias",
        null,
        null,
        null,
        Array.Empty<int>(),
        TableFunctionSchema.AliasColumnList);

    public static TvfEntityBuilder<ISimpleEntity> CustomGenerateSeries(long start, long stop) => new("generate_series");
}

/// <summary>
/// #320 (T320) SQL-generation parity: the runtime factory route must render exactly the statement the
/// first factory declaration renders for identical metadata, and a user value must never be injected
/// into the statement text. No database connection is opened: the SQLite connection string is a placeholder.
/// </summary>
[Trait("Category", "T320")]
public class T320RuntimeTvfSqlTests(ITestOutputHelper output)
{
    private static (string Sql, List<string> Parameters) Render<T>(IDataContext ctx, QueryCommand<T> command)
    {
        var prepared = (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(command, false, false, CancellationToken.None);

        return (
            prepared.DbCommand.CommandText.Replace("\r\n", "\n"),
            [.. prepared.DbCommandParams.Cast<DbParameter>().Select(p => $"{p.ParameterName}={p.Value ?? "null"}")]);
    }

    /// <summary>Writes the two rendered statements and the ordered parameter lists and returns the same text.</summary>
    private string Report(
        string legacySql,
        List<string> legacyParameters,
        string runtimeSql,
        List<string> runtimeParameters)
    {
        var text =
            $"legacy sql: <<{legacySql}>>\n"
            + $"legacy parameters: [{string.Join(", ", legacyParameters)}]\n"
            + $"runtime sql: <<{runtimeSql}>>\n"
            + $"runtime parameters: [{string.Join(", ", runtimeParameters)}]";

        output.WriteLine(text);
        return text;
    }

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> command, bool storeInCache)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(command, false, storeInCache, CancellationToken.None);

    private static List<string> ParametersOf<T>(DbPreparedQueryCommand<T> prepared)
        => [.. prepared.DbCommandParams.Cast<DbParameter>().Select(p => $"{p.ParameterName}={p.Value ?? "null"}")];

    [Fact]
    public void RuntimeTvf_WithLiteralArgument_ShouldRenderTheSameStatementAsTheLegacyTvf()
    {
        using var ctx = SqliteTestContext.Create();

        var (legacySql, legacyParameters) =
            Render(ctx, ctx.From(() => T320ProbeTvf.LegacyProbe(42)).Select(r => new { r.Id }));
        var (runtimeSql, runtimeParameters) =
            Render(ctx, ctx.From(() => T320ProbeTvf.ProbeTvf(42)).Select(r => new { r.Id }));

        var observed = Report(legacySql, legacyParameters, runtimeSql, runtimeParameters);

        runtimeSql.Should().Be(legacySql, $"identical metadata must render an identical statement; {observed}");
        runtimeParameters.Should().Equal(legacyParameters, observed);
        runtimeSql.Should().Contain("from probe_tvf", observed);
        // Recorded observation for a *literal* argument: both routes inline it as a SQL literal rather
        // than binding a parameter (only a captured value becomes a parameter - see the next fact).
        runtimeSql.Should().Contain("42", observed);
    }

    [Fact]
    public void RuntimeTvf_WithCapturedArgument_ShouldBindAParameterAndMatchTheLegacyTvf()
    {
        using var ctx = SqliteTestContext.Create();
        var id = 42;

        var (legacySql, legacyParameters) =
            Render(ctx, ctx.From(() => T320ProbeTvf.LegacyProbe(id)).Select(r => new { r.Id }));
        var (runtimeSql, runtimeParameters) =
            Render(ctx, ctx.From(() => T320ProbeTvf.ProbeTvf(id)).Select(r => new { r.Id }));

        var observed = Report(legacySql, legacyParameters, runtimeSql, runtimeParameters);

        runtimeSql.Should().Be(legacySql, $"a capture must be lowered identically on both routes; {observed}");
        runtimeParameters.Should().Equal(legacyParameters, $"a capture is a parameter on both routes; {observed}");
        runtimeParameters.Should().ContainSingle($"a capture becomes one bound parameter, never an inlined value; {observed}");
        runtimeParameters[0].Should().Be("id=42", "the parameter carries the captured value, not the statement text");
        runtimeSql.Should().NotContain("42", $"a captured value must not be inlined into the statement text; {observed}");
    }

    [Fact]
    public void RuntimeTvf_WithCapturedHostileString_ShouldKeepItOutOfTheStatementText()
    {
        using var ctx = SqliteTestContext.Create();
        var text = "x'; DROP TABLE t; --";

        var (legacySql, legacyParameters) =
            Render(ctx, ctx.From(() => T320ProbeTvf.LegacyProbeText(text)).Select(r => new { r.Id }));
        var (runtimeSql, runtimeParameters) =
            Render(ctx, ctx.From(() => T320ProbeTvf.ProbeTvfText(text)).Select(r => new { r.Id }));

        var observed = Report(legacySql, legacyParameters, runtimeSql, runtimeParameters);

        runtimeSql.Should().Be(legacySql, $"identical metadata must render an identical statement; {observed}");
        runtimeParameters.Should().Equal(legacyParameters, $"the hostile value travels as a bound parameter on both routes; {observed}");
        runtimeParameters.Should().ContainSingle(observed);
        runtimeParameters[0].Should().Be($"text={text}", "the value is only in the parameter, verbatim and unevaluated");
        runtimeSql.Should().NotContain("DROP TABLE", $"a captured value must never reach the statement text; {observed}");
        runtimeSql.Should().NotContain("x'", $"the quoting character of the value must not appear outside the parameter; {observed}");
    }

    [Fact]
    public void RuntimeTvf_WithLiteralHostileString_ShouldEscapeRatherThanInject()
    {
        using var ctx = SqliteTestContext.Create();

        var (legacySql, legacyParameters) =
            Render(ctx, ctx.From(() => T320ProbeTvf.LegacyProbeText("x'; DROP TABLE t; --")).Select(r => new { r.Id }));
        var (runtimeSql, runtimeParameters) =
            Render(ctx, ctx.From(() => T320ProbeTvf.ProbeTvfText("x'; DROP TABLE t; --")).Select(r => new { r.Id }));

        var observed = Report(legacySql, legacyParameters, runtimeSql, runtimeParameters);

        // Recorded observation: a *literal* string argument is an inline, dialect-escaped SQL literal on
        // both routes (only a captured value is bound as a parameter). The safety property that holds for
        // both shapes is that the raw injection string never appears unescaped in the statement.
        runtimeSql.Should().Be(legacySql, $"identical metadata must render an identical statement; {observed}");
        runtimeParameters.Should().Equal(legacyParameters, observed);
        runtimeSql.Should().NotContain(
            "x'; DROP TABLE t; --",
            $"the apostrophe must be escaped so the value cannot terminate the literal; {observed}");
        runtimeSql.Should().Contain("''", $"the literal is escaped by doubling the quote; {observed}");
    }

    // ---- AC08/AC09: SQL identity under the plan cache ------------------------------------------

    [Fact]
    public void RuntimeTvf_DifferentSqlIdentities_ShouldNotShareACachedPlan()
    {
        using var ctx = SqliteTestContext.Create();
        ctx.PurgeQueryCache();

        string Own(Func<QueryCommand<int>> build) => Normalize(Prepare(ctx, build(), false).DbCommand.CommandText);
        string Cached(Func<QueryCommand<int>> build) => Normalize(Prepare(ctx, build(), true).DbCommand.CommandText);

        var variants = new (string Name, Func<QueryCommand<int>> Build)[]
        {
            ("alpha", () => ctx.From(() => T320ProbeTvf.IdentityAlpha("raw")).Select(r => r.Id)),
            ("beta", () => ctx.From(() => T320ProbeTvf.IdentityBeta("raw")).Select(r => r.Id)),
            ("schema", () => ctx.From(() => T320ProbeTvf.IdentityOtherSchema("raw")).Select(r => r.Id)),
            ("verbatim", () => ctx.From(() => T320ProbeTvf.IdentityVerbatim("raw")).Select(r => r.Id)),
        };

        var ownSql = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, build) in variants)
            ownSql[name] = Own(build);

        ownSql.Values.Should().OnlyHaveUniqueItems("each runtime SQL identity has its own statement");

        foreach (var (name, build) in variants)
        {
            var first = Cached(build);
            var second = Cached(build);

            first.Should().Be(ownSql[name], $"'{name}' must keep its own SQL on a cache miss; {first}");
            second.Should().Be(ownSql[name], $"'{name}' must keep its own SQL after a cache hit, never another identity's; {second}");
        }

        ownSql["alpha"].Should().Contain("dbo.identity_alpha");
        ownSql["beta"].Should().Contain("dbo.identity_beta");
        ownSql["schema"].Should().Contain("identity_other.identity_alpha");
        ownSql["verbatim"].Should().Contain("(raw)");
    }

    [Fact]
    public void RuntimeTvf_EquivalentSqlIdentities_ShouldRenderTheSameStatement()
    {
        using var ctx = SqliteTestContext.Create();
        ctx.PurgeQueryCache();
        var id = 5;

        var one = Prepare(ctx, ctx.From(() => T320ProbeTvf.EquivalentOne(id)).Select(r => r.Id), true);
        var two = Prepare(ctx, ctx.From(() => T320ProbeTvf.EquivalentTwo(id)).Select(r => r.Id), true);

        Normalize(two.DbCommand.CommandText).Should().Be(
            Normalize(one.DbCommand.CommandText),
            "equivalent runtime metadata and argument shapes must render the same statement");
        ParametersOf(two).Should().Equal(ParametersOf(one), "the equivalent sources bind the same parameter");
        ParametersOf(two).Should().Equal("id=5");
    }

    /// <summary>
    /// AC08 at the real plan store: two runtime factory declarations carry the same SQL identity, so on
    /// one context the second preparation must hit the first one's single stored plan entry instead of
    /// adding another, and both must render the same statement.
    /// </summary>
    [Fact]
    public void RuntimeTvf_AndLegacyTvf_WithTheSameSqlIdentity_ShouldLeaveOnePlanStoreEntry()
    {
        using var ctx = SqliteTestContext.Create();
        ctx.PurgeQueryCache();

        var legacy = Prepare(ctx, ctx.From(() => T320ProbeTvf.LegacyProbe(42)).Select(r => r.Id), true);
        var legacySql = Normalize(legacy.DbCommand.CommandText);
        var plansAfterLegacy = QueryPlanStore.Values.Count();
        output.WriteLine(
            $"plan store: count after legacy={plansAfterLegacy}; legacy sql=<<{legacySql}>>; "
            + $"legacy parameters=[{string.Join(", ", ParametersOf(legacy))}]; "
            + $"legacy plan identity hash={RuntimeHelpers.GetHashCode(legacy)}");
        plansAfterLegacy.Should().Be(1, "the legacy route stores exactly one plan entry");

        var runtime = Prepare(ctx, ctx.From(() => T320ProbeTvf.ProbeTvf(42)).Select(r => r.Id), true);
        var runtimeSql = Normalize(runtime.DbCommand.CommandText);
        var plansAfterRuntime = QueryPlanStore.Values.Count();
        var sharedPlan = ReferenceEquals(legacy, runtime);
        output.WriteLine(
            $"plan store: count after runtime={plansAfterRuntime}; ReferenceEquals(legacy, runtime)={sharedPlan}; "
            + $"runtime sql=<<{runtimeSql}>>; runtime parameters=[{string.Join(", ", ParametersOf(runtime))}]; "
            + $"runtime plan identity hash={RuntimeHelpers.GetHashCode(runtime)}");

        QueryPlanStore.Values.Count().Should().Be(
            plansAfterLegacy,
            "the runtime route with the same SQL identity must reuse the legacy plan entry, not add another");
        ReferenceEquals(legacy, runtime).Should().BeTrue(
            "the runtime preparation must hit the legacy plan instance");
        runtimeSql.Should().Be(legacySql, "both routes must render an identical statement");
        ParametersOf(runtime).Should().Equal(ParametersOf(legacy), "both routes bind the same parameters");
    }

    [Fact]
    public void RuntimeTvf_CacheHit_ShouldRefreshTheCapturedFromArgument()
    {
        using var ctx = SqliteTestContext.Create();
        ctx.PurgeQueryCache();
        var id = 1;

        QueryCommand<int> Build() => ctx.From(() => T320ProbeTvf.ProbeTvf(id)).Select(r => r.Id);

        var first = Prepare(ctx, Build(), true);
        var firstSql = Normalize(first.DbCommand.CommandText);
        var firstParameters = ParametersOf(first);
        output.WriteLine(
            $"refresh run 1: sql=<<{firstSql}>>; parameters=[{string.Join(", ", firstParameters)}]; "
            + $"plan identity hash={RuntimeHelpers.GetHashCode(first)}");
        firstParameters.Should().Equal("id=1");

        id = 2;
        var second = Prepare(ctx, Build(), true);
        var secondParameters = ParametersOf(second);
        output.WriteLine(
            $"refresh run 2: sql=<<{Normalize(second.DbCommand.CommandText)}>>; "
            + $"parameters=[{string.Join(", ", secondParameters)}]; "
            + $"ReferenceEquals(run1, run2)={ReferenceEquals(first, second)}; "
            + $"plan identity hash={RuntimeHelpers.GetHashCode(second)}");

        ReferenceEquals(first, second).Should().BeTrue(
            "the same context and query shape must reuse the cached plan so the refresh path is exercised");
        Normalize(second.DbCommand.CommandText).Should().Be(firstSql, "a capture change must not re-render the statement");
        ParametersOf(second).Should().Equal(new[] { "id=2" }, "the cache hit must bind the new capture, never the stale 1");
        ParametersOf(second).Should().ContainSingle("the parameter name and slot count stay unchanged");
    }

    [Fact]
    public void RuntimeTvf_TwoSourcesInOneQuery_ShouldBindBothAndRefreshOnlyTheChangedSlot()
    {
        using var ctx = SqliteTestContext.Create();
        ctx.PurgeQueryCache();
        var outerId = 1;
        var innerId = 2;

        QueryCommand<int> Build() => ctx.From(() => T320ProbeTvf.JoinSource(outerId))
            .Join(() => T320ProbeTvf.InnerSource(innerId), (left, right) => left.Id == right.Id)
            .Select(p => p.Item1.Id);

        var first = Prepare(ctx, Build(), true);
        var firstSql = Normalize(first.DbCommand.CommandText);
        ParametersOf(first).Should().Equal("outerId=1", "innerId=2");
        firstSql.Should().Contain("join_probe").And.Contain("inner_probe");

        outerId = 5;
        var second = Prepare(ctx, Build(), true);

        ReferenceEquals(first, second).Should().BeTrue("the two-source shape must reuse the cached plan");
        Normalize(second.DbCommand.CommandText).Should().Be(firstSql, "the statement must not change when a capture changes");
        ParametersOf(second).Should().Equal(
            new[] { "outerId=5", "innerId=2" },
            "the refresh must update exactly the changed slot and keep the other one");
    }

    [Fact]
    public void RuntimeTvf_OrdinaryJoinSource_ShouldRefreshTheJoinCapture()
    {
        using var ctx = SqliteTestContext.Create();
        ctx.PurgeQueryCache();
        var joinId = 3;

        QueryCommand<int> Build() => ctx.From<ISimpleEntity>()
            .Join(() => T320ProbeTvf.JoinSource(joinId), (left, right) => left.Id == right.Id)
            .Select(p => p.Item1.Id);

        var first = Prepare(ctx, Build(), true);
        var firstSql = Normalize(first.DbCommand.CommandText);
        ParametersOf(first).Should().Equal("joinId=3");

        joinId = 4;
        var second = Prepare(ctx, Build(), true);

        ReferenceEquals(first, second).Should().BeTrue("an ordinary join over a runtime TVF must reuse its cached plan");
        Normalize(second.DbCommand.CommandText).Should().Be(firstSql);
        ParametersOf(second).Should().Equal(new[] { "joinId=4" }, "the join-source capture must refresh on a cache hit");
    }

    [Fact]
    public void RuntimeTvf_TwoContexts_ShouldIsolateValuesAndKeepTheSharedAnyCommandCacheable()
    {
        using var ctxA = SqliteTestContext.CreateSqlite();
        using var ctxB = SqliteTestContext.CreateSqlite();
        ctxA.PurgeQueryCache();

        var anyA = EntityBuilderExtensions.GetAnyCommand(ctxA, ctxA.From<SimpleEntity>().ToCommand());
        var anyB = EntityBuilderExtensions.GetAnyCommand(ctxB, ctxB.From<SimpleEntity>().ToCommand());
        anyA.Cache.Should().BeTrue("the shared Any command starts cacheable");
        anyB.Cache.Should().BeTrue("the shared Any command starts cacheable");
        output.WriteLine($"two contexts: before preparation AnyCommand.Cache A={anyA.Cache}; B={anyB.Cache}");

        var id = 11;
        QueryCommand<int> Build(IDataContext context) => context.From(() => T320ProbeTvf.ProbeTvf(id)).Select(r => r.Id);

        var a1 = ParametersOf(Prepare(ctxA, Build(ctxA), true));
        id = 22;
        var b1 = ParametersOf(Prepare(ctxB, Build(ctxB), true));
        id = 11;
        var a2 = ParametersOf(Prepare(ctxA, Build(ctxA), true));

        output.WriteLine(
            $"two contexts: A run 1=[{string.Join(", ", a1)}]; B run 1=[{string.Join(", ", b1)}]; "
            + $"A run 2=[{string.Join(", ", a2)}]; "
            + $"ctxA.AnyCommand.Cache={ctxA.AnyCommand!.Value.Cache}; ctxB.AnyCommand.Cache={ctxB.AnyCommand!.Value.Cache}; "
            + $"ReferenceEquals(ctxA.AnyCommand, ctxB.AnyCommand)={ReferenceEquals(ctxA.AnyCommand, ctxB.AnyCommand)}");

        a1.Should().Equal("id=11");
        b1.Should().Equal("id=22");
        a2.Should().Equal(new[] { "id=11" }, "the second context's value must not leak back into the first");

        ctxA.AnyCommand!.Value.Cache.Should().BeTrue(
            "TVF preparation must not set the sticky QueryCommand.Cache=false flag on the shared Any command");
        ctxB.AnyCommand!.Value.Cache.Should().BeTrue(
            "TVF preparation must not set the sticky QueryCommand.Cache=false flag on the shared Any command");
        ReferenceEquals(ctxA.AnyCommand, ctxB.AnyCommand).Should().BeFalse("each context owns its shared Any command");
    }

    // ---- F-04: composed descriptor chains render the argument and the composition ----------------

    /// <summary>
    /// A runtime factory call composed with <c>Where</c> must render the originating call — with its own
    /// argument — and the composed predicate on the same statement, not a bare call and not a rejected
    /// expression.
    /// </summary>
    [Fact]
    public void RuntimeTvf_ComposedWhereChain_ShouldRenderTheArgumentAndThePredicate()
    {
        using var ctx = SqliteTestContext.Create();
        var id = 42;

        var prepared = Prepare(
            ctx,
            ctx.From(() => T320ProbeTvf.ProbeTvf(id).Where(r => r.Id > 10)).Select(r => r.Id),
            false);
        var sql = Normalize(prepared.DbCommand.CommandText);
        var parameters = ParametersOf(prepared);
        output.WriteLine($"composed where: sql=<<{sql}>>; parameters=[{string.Join(", ", parameters)}]");

        sql.Should().Contain("from probe_tvf($id)", $"the originating factory call keeps its own argument; {sql}");
        sql.Should().Contain("where", $"the composed predicate is applied to the same statement; {sql}");
        parameters.Should().Equal(new[] { "id=42" }, $"the TVF argument stays a bound parameter of the call; {sql}");
    }

    /// <summary>The composed ordering must survive lowering on the same statement as the call.</summary>
    [Fact]
    public void RuntimeTvf_ComposedOrderingChain_ShouldKeepTheOrderingClause()
    {
        using var ctx = SqliteTestContext.Create();
        var id = 42;

        var prepared = Prepare(ctx, ctx.From(() => T320ProbeTvf.ProbeTvf(id).OrderBy(r => r.Id)).Select(r => r.Id), false);
        var sql = Normalize(prepared.DbCommand.CommandText);
        var parameters = ParametersOf(prepared);
        output.WriteLine($"composed ordering: sql=<<{sql}>>; parameters=[{string.Join(", ", parameters)}]");

        sql.Should().Contain("from probe_tvf($id)", $"the originating factory call keeps its own argument; {sql}");
        sql.Should().Contain("order by", $"the composed ordering is preserved; {sql}");
        parameters.Should().Equal(new[] { "id=42" });
    }

    /// <summary>
    /// A type-changing projection composed on the descriptor (the factory row type differs from the
    /// composed result type) must still carry the factory call and its argument.
    /// </summary>
    [Fact]
    public void RuntimeTvf_ComposedTypeChangingProjection_ShouldKeepTheSourceAndItsArgument()
    {
        using var ctx = SqliteTestContext.Create();
        var id = 42;

        var source = ctx.From(() => T320ProbeTvf.ProbeTvf(id).As(r => new { r.Id }));
        var prepared = Prepare(ctx, source.Select(r => r.Id), false);
        var sql = Normalize(prepared.DbCommand.CommandText);
        var parameters = ParametersOf(prepared);
        output.WriteLine($"composed projection: sql=<<{sql}>>; parameters=[{string.Join(", ", parameters)}]");

        sql.Should().Contain("probe_tvf($id)", $"the factory call and its argument survive the projection; {sql}");
        parameters.Should().Equal(new[] { "id=42" });
    }

    // ---- AC04/AC07: provider restriction --------------------------------------------------------

    /// <summary>
    /// A correlated runtime-TVF APPLY on a dialect with no lateral source is rejected with the named
    /// dialect exception instead of being rendered as an ordinary join or silently dropped.
    /// </summary>
    [Fact]
    public void CorrelatedRuntimeTvfApply_WithoutApplySupport_ShouldBeRejected()
    {
        using var ctx = SqliteTestContext.Create();

        Action act = () => Render(
            ctx,
            ctx.From<ISimpleEntity>()
                .CrossApply(row => T320ProbeTvf.ProbeTvf(row.Id))
                .Select(p => p.Item1.Id));

        act.Should().Throw<NotSupportedException>().WithMessage("*CrossApply join is not supported*");
    }

    // ---- MX-SQ: the remaining matrix cells (N/X/S/U/R/F) ------------------------------------------

    [Fact]
    public void RuntimeTvf_N_NullArgument_ShouldRenderNullWithoutAParameter()
    {
        using var ctx = SqliteTestContext.Create();

        var prepared = Prepare(ctx, ctx.From(() => T320ProbeTvf.ProbeTvfText(null)).Select(r => r.Id), false);
        var sql = Normalize(prepared.DbCommand.CommandText);
        var parameters = ParametersOf(prepared);
        output.WriteLine($"null: sql=<<{sql}>>; parameters=[{string.Join(", ", parameters)}]");

        sql.Should().Contain("from probe_tvf_text(null)", "a null argument stays the SQL NULL literal");
        parameters.Should().BeEmpty("a null literal is not replaced by a metadata-evaluation default");
    }

    [Fact]
    public void RuntimeTvf_X_NestedSqlFunctionArgument_ShouldKeepTheNestedCallAndTheParameter()
    {
        using var ctx = SqliteTestContext.Create();
        var id = 42;

        var prepared = Prepare(
            ctx,
            ctx.From(() => T320ProbeTvf.ProbeTvf(SqlFunctions.Sql.nullif(id, 0))).Select(r => r.Id),
            false);
        var sql = Normalize(prepared.DbCommand.CommandText);
        var parameters = ParametersOf(prepared);
        output.WriteLine($"nested: sql=<<{sql}>>; parameters=[{string.Join(", ", parameters)}]");

        sql.Should().Contain("probe_tvf(nullif($id, 0))", "the nested SQL function is the argument, not its value");
        sql.Should().NotContain("probe_tvf(42)", "the nested expression is never evaluated into a constant");
        parameters.Should().Equal(new[] { "id=42" }, "the capture inside the nested call is a bound parameter");
    }

    [Fact]
    public void RuntimeTvf_S_TypeChangingSelect_ShouldKeepTheSourceAndItsArgument()
    {
        using var ctx = SqliteTestContext.Create();
        var id = 42;

        // The composed projection changes the builder row type from the factory row type ISimpleEntity
        // to an anonymous shape: the source and its argument must survive that difference.
        var prepared = Prepare(
            ctx,
            ctx.From(() => T320ProbeTvf.ProbeTvf(id)).Select(r => new { r.Id, Doubled = r.Id * 2 }),
            false);
        var sql = Normalize(prepared.DbCommand.CommandText);
        var parameters = ParametersOf(prepared);
        output.WriteLine($"projection: sql=<<{sql}>>; parameters=[{string.Join(", ", parameters)}]");

        sql.Should().Contain("from probe_tvf($id)", "the factory call and its argument survive the type-changing projection");
        sql.Should().Contain("* 2", "the type-changing projection stays on the same statement");
        parameters.Should().Equal(new[] { "id=42" });
    }

    [Fact]
    public void RuntimeTvf_U_CloneUpcastAndReuse_ShouldKeepTheSourceAndItsArgument()
    {
        using var ctx = SqliteTestContext.Create();
        var id = 7;

        // Upcast: the descriptor is usable through the EntityBuilder<T> static type.
        TvfEntityBuilder<ISimpleEntity> descriptor = T320ProbeTvf.ProbeTvf(id);
        EntityBuilder<ISimpleEntity> upcast = descriptor;
        var clone = upcast.Clone();

        clone.Should().BeAssignableTo<EntityBuilder<ISimpleEntity>>();
        clone.Should().BeOfType<TvfEntityBuilder<ISimpleEntity>>("the clone keeps the runtime descriptor type");
        clone.Should().NotBeSameAs(upcast, "the clone is a copy, not the same builder");
        var cloned = (TvfEntityBuilder<ISimpleEntity>)clone;
        cloned.Name.Should().Be("probe_tvf");
        cloned.Schema.Should().BeNull();
        cloned.ResultSchema.Should().Be(TableFunctionSchema.None);
        cloned.VerbatimArguments.Should().BeEmpty();

        // Reuse: two independent consumers of the same source shape render the same statement.
        var first = Prepare(ctx, ctx.From(() => T320ProbeTvf.ProbeTvf(id)).Select(r => r.Id), false);
        var second = Prepare(ctx, ctx.From(() => T320ProbeTvf.ProbeTvf(id)).Select(r => r.Id), false);
        var firstSql = Normalize(first.DbCommand.CommandText);
        var secondSql = Normalize(second.DbCommand.CommandText);
        output.WriteLine($"reuse: first=<<{firstSql}>>; second=<<{secondSql}>>; parameters=[{string.Join(", ", ParametersOf(second))}]");

        secondSql.Should().Be(firstSql, "reusing the same factory/descriptor shape renders the same statement");
        ParametersOf(second).Should().Equal(ParametersOf(first));
        ParametersOf(second).Should().Equal(new[] { "id=7" });
    }

    [Fact]
    public void RuntimeTvf_R_ResultPlacements_ShouldRenderNoneAndRefuseBothPlacements()
    {
        using var ctx = SqliteTestContext.Create();

        // None: the function supplies its own result shape, so the call is bare — no leading argument and
        // no alias column-definition list.
        var none = Prepare(ctx, ctx.From(() => T320ProbeTvf.ProbeTvf(42)).Select(r => r.Id), false);
        var noneSql = Normalize(none.DbCommand.CommandText);
        output.WriteLine($"placement none: sql=<<{noneSql}>>");
        noneSql.Should().Contain("from probe_tvf(42)", "None renders the call itself");
        noneSql.Should().NotContain(" with (", "None appends no WITH clause");

        // SQLite expresses neither a leading column-definition argument nor an alias column list, so both
        // placements are named capability refusals, not silently dropped.
        Action leading = () => Prepare(ctx, ctx.From(() => T320ProbeTvf.LeadingResultSchema(42)).Select(r => r.Id), false);
        leading.Should().Throw<NotSupportedException>()
            .WithMessage("*'t320_leading'*'LeadingArgument' form*not supported by this provider*");

        Action alias = () => Prepare(ctx, ctx.From(() => T320ProbeTvf.AliasResultSchema(42)).Select(r => r.Id), false);
        alias.Should().Throw<NotSupportedException>()
            .WithMessage("*'t320_alias'*'AliasColumnList' form*not supported by this provider*");
    }

    [Fact]
    public void RuntimeTvf_F_BuiltInJsonEachVersusCustomFactory_ShouldStayDistinct()
    {
        using var ctx = SqliteTestContext.Create();
        var json = "[1,2,3]";

        // Built-in: json_each is in the SQLite built-in table-function capability list and renders through
        // the built-in path with the captured document bound as a parameter.
        var builtIn = Prepare(ctx, ctx.From(() => SqlFunctions.Sqlite.json_each(json)).Select(r => r.Value), false);
        var builtInSql = Normalize(builtIn.DbCommand.CommandText);
        output.WriteLine($"built-in json_each: sql=<<{builtInSql}>>; parameters=[{string.Join(", ", ParametersOf(builtIn))}]");
        builtInSql.Should().Contain("from json_each($json)", "the built-in table function renders its native call");
        ParametersOf(builtIn).Should().Equal(new[] { "json=[1,2,3]" });

        // Custom: the same SQL name through a runtime factory is a user-defined function, emitted verbatim
        // even when the name is not in the built-in capability list (generate_series on SQLite).
        var custom = Prepare(ctx, ctx.From(() => T320ProbeTvf.CustomGenerateSeries(1, 3)).Select(r => r.Id), false);
        var customSql = Normalize(custom.DbCommand.CommandText);
        output.WriteLine($"custom generate_series: sql=<<{customSql}>>");
        customSql.Should().Contain("from generate_series(1, 3)", "a custom runtime factory is rendered verbatim");

        // The built-in surface stays gated: SQLite has no generate_series table function.
        Action builtInGate = () => Prepare(
            ctx,
            ctx.From(() => SqlFunctions.Postgres.generate_series(1, 3)).Select(r => r.Value),
            false);
        builtInGate.Should().Throw<NotSupportedException>()
            .WithMessage("*'generate_series'*not supported by this provider*");
    }

    // ---- MX-CHAIN / AC05: the argument survives every supported copy path -------------------------

    /// <summary>
    /// A byte-for-byte identical parameter list for the same captured value: a null value is kept as a
    /// parameter slot with a <c>DBNull</c> value (unlike a *literal* null, which is an inline SQL NULL).
    /// </summary>
    private static List<string> ParameterValues<T>(DbPreparedQueryCommand<T> prepared)
        => [.. prepared.DbCommandParams.Cast<DbParameter>()
            .Select(p => p.Value is null or DBNull ? $"{p.ParameterName}=<null>" : $"{p.ParameterName}={p.Value}")];

    /// <summary>
    /// MX-CHAIN (AC05): SQLite expresses no lateral source, so the only correlation a runtime TVF argument
    /// can carry is a captured value bound to a parameter slot. That slot must survive every supported copy
    /// path — the composed <c>Where</c>, the composed ordering, a type-changing <c>Select</c>, a clone of the
    /// lowered builder and an upcast of it — with the same statement and the same bound value.
    /// </summary>
    [Fact]
    public void RuntimeTvf_Chain_CorrelatedCapture_ShouldSurviveEverySupportedCopyPath()
    {
        using var ctx = SqliteTestContext.Create();
        var id = 42;

        var where = Prepare(ctx, ctx.From(() => T320ProbeTvf.ProbeTvf(id).Where(r => r.Id > 10)).Select(r => r.Id), false);
        var order = Prepare(ctx, ctx.From(() => T320ProbeTvf.ProbeTvf(id).OrderBy(r => r.Id)).Select(r => r.Id), false);
        var projection = Prepare(ctx, ctx.From(() => T320ProbeTvf.ProbeTvf(id)).Select(r => new { r.Id, Doubled = r.Id * 2 }), false);

        var lowered = ctx.From(() => T320ProbeTvf.ProbeTvf(id));
        var clone = (EntityBuilder<ISimpleEntity>)lowered.Clone();
        clone.SourceFrom.Should().NotBeSameAs(null, "the clone carries the lowered table-function source");
        var cloned = Prepare(ctx, clone.Select(r => r.Id), false);

        EntityBuilder<ISimpleEntity> upcast = ctx.From(() => T320ProbeTvf.ProbeTvf(id));
        var upcasted = Prepare(ctx, upcast.Select(r => new { r.Id, Doubled = r.Id * 2 }), false);

        var paths = new (string Name, string Sql, List<string> Parameters)[]
        {
            ("where", Normalize(where.DbCommand.CommandText), ParametersOf(where)),
            ("ordering", Normalize(order.DbCommand.CommandText), ParametersOf(order)),
            ("type-changing select", Normalize(projection.DbCommand.CommandText), ParametersOf(projection)),
            ("clone", Normalize(cloned.DbCommand.CommandText), ParametersOf(cloned)),
            ("upcast", Normalize(upcasted.DbCommand.CommandText), ParametersOf(upcasted)),
        };

        foreach (var (name, sql, parameters) in paths)
            output.WriteLine($"copy path {name}: sql=<<{sql}>>; parameters=[{string.Join(", ", parameters)}]");

        foreach (var (name, sql, parameters) in paths)
        {
            sql.Should().Contain("from probe_tvf($id)", $"the '{name}' path keeps the originating call and its argument; {sql}");
            sql.Should().NotContain("probe_tvf(42)", $"the '{name}' path must never evaluate the capture into the statement; {sql}");
            parameters.Should().Equal(new[] { "id=42" }, $"the '{name}' path binds the same captured value to the same slot");
        }

        paths.Select(p => p.Sql).Where(sql => !sql.Contains("* 2")).Should().OnlyHaveUniqueItems(
            "each copy path renders its own composed statement");
        paths[2].Sql.Should().Be(paths[4].Sql, "the upcast renders the same projection statement the direct consumer does");
        paths.Select(p => string.Join("|", p.Parameters)).Should().OnlyContain(text => text == "id=42");
    }

    /// <summary>
    /// MX-CHAIN (AC05): two consumers on two different contexts each keep their own captured value — no
    /// cross-consumer or cross-context leak — including the consumer that renders through a clone of its own
    /// lowered builder, and the shared <c>AnyCommand.Cache</c> stays untouched on both contexts.
    /// </summary>
    [Fact]
    public void RuntimeTvf_Chain_TwoConsumersOnTwoContexts_ShouldKeepTheirOwnCaptureAndLeaveAnyCommandCacheable()
    {
        using var ctxA = SqliteTestContext.CreateSqlite();
        using var ctxB = SqliteTestContext.CreateSqlite();
        ctxA.PurgeQueryCache();

        var anyA = EntityBuilderExtensions.GetAnyCommand(ctxA, ctxA.From<SimpleEntity>().ToCommand());
        var anyB = EntityBuilderExtensions.GetAnyCommand(ctxB, ctxB.From<SimpleEntity>().ToCommand());
        anyA.Cache.Should().BeTrue("the shared Any command starts cacheable");
        anyB.Cache.Should().BeTrue("the shared Any command starts cacheable");

        var aId = 11;
        var bId = 22;

        var consumerA = Prepare(ctxA, ctxA.From(() => T320ProbeTvf.ProbeTvf(aId)).Select(r => r.Id), true);
        var consumerB = Prepare(ctxB, ctxB.From(() => T320ProbeTvf.ProbeTvf(bId)).Select(r => r.Id), true);

        var cloneA = (EntityBuilder<ISimpleEntity>)ctxA.From(() => T320ProbeTvf.ProbeTvf(aId)).Clone();
        var clonedA = Prepare(ctxA, cloneA.Select(r => r.Id), true);

        var parametersA = ParametersOf(consumerA);
        var parametersB = ParametersOf(consumerB);
        var parametersClonedA = ParametersOf(clonedA);
        output.WriteLine(
            $"two consumers: A=[{string.Join(", ", parametersA)}] sql=<<{Normalize(consumerA.DbCommand.CommandText)}>>; "
            + $"B=[{string.Join(", ", parametersB)}] sql=<<{Normalize(consumerB.DbCommand.CommandText)}>>; "
            + $"A-clone=[{string.Join(", ", parametersClonedA)}] sql=<<{Normalize(clonedA.DbCommand.CommandText)}>>; "
            + $"ctxA.AnyCommand.Cache={ctxA.AnyCommand!.Value.Cache}; ctxB.AnyCommand.Cache={ctxB.AnyCommand!.Value.Cache}");

        parametersA.Should().Equal(new[] { "aId=11" }, "each consumer binds its own captured value");
        parametersB.Should().Equal(new[] { "bId=22" }, "the second context's consumer keeps its own value, never the first one's");
        parametersClonedA.Should().Equal(new[] { "aId=11" }, "a consumer rendering through a clone keeps its own capture");

        Normalize(consumerA.DbCommand.CommandText).Should().Contain("probe_tvf($aId)");
        Normalize(consumerB.DbCommand.CommandText).Should().Contain("probe_tvf($bId)");
        Normalize(clonedA.DbCommand.CommandText).Should().Be(
            Normalize(consumerA.DbCommand.CommandText),
            "the clone renders the same statement as the direct consumer on the same context");

        ctxA.AnyCommand!.Value.Cache.Should().BeTrue(
            "TVF preparation must not set the sticky QueryCommand.Cache=false flag on the shared Any command");
        ctxB.AnyCommand!.Value.Cache.Should().BeTrue(
            "TVF preparation must not set the sticky QueryCommand.Cache=false flag on the shared Any command");
    }

    // ---- MX-CACHE: FromOptions variants and null transitions -------------------------------------

    /// <summary>
    /// MX-CACHE: <c>FromOptions</c> applied to a runtime TVF source is either SQL-identity-neutral or a named
    /// refusal — it must never silently share an incompatible plan. A derived-table hint and a table sample
    /// have no place on a function source, so they are dropped and the source keeps the one plan entry it
    /// would have without them; a source-level row lock is refused by name.
    /// </summary>
    [Fact]
    public void RuntimeTvf_Cache_FromOptionsVariants_ShouldBeSqlIdentityNeutralOrFailClosed()
    {
        using var ctx = SqliteTestContext.Create();
        ctx.PurgeQueryCache();
        var id = 42;

        var plain = Prepare(ctx, ctx.From(() => T320ProbeTvf.ProbeTvf(id)).Select(r => r.Id), true);
        var plainSql = Normalize(plain.DbCommand.CommandText);
        var plansAfterPlain = QueryPlanStore.Values.Count();
        plansAfterPlain.Should().Be(1, "the plain source stores one plan entry");

        // WithSubQueryHint: a derived-table hint describes a subquery; a table-function source is not one,
        // so the hint is not rendered and the plan identity is unchanged (recorded as neutral, with reason).
        var hinted = Prepare(
            ctx,
            ctx.From(() => T320ProbeTvf.ProbeTvf(id), options => options.WithSubQueryHint("NestLoop(t1)")).Select(r => r.Id),
            true);
        var hintedSql = Normalize(hinted.DbCommand.CommandText);
        output.WriteLine($"FromOptions hint: sql=<<{hintedSql}>>; parameters=[{string.Join(", ", ParametersOf(hinted))}]");

        // TableSample: a table sample applies to a physical table, not to a function source, so it is
        // dropped for the same reason and stays plan-neutral.
        var sampled = Prepare(
            ctx,
            ctx.From(() => T320ProbeTvf.ProbeTvf(id), options => options.TableSample(50)).Select(r => r.Id),
            true);
        var sampledSql = Normalize(sampled.DbCommand.CommandText);
        output.WriteLine($"FromOptions sample: sql=<<{sampledSql}>>; parameters=[{string.Join(", ", ParametersOf(sampled))}]");

        hintedSql.Should().Be(plainSql, "a derived-table hint is SQL-identity-neutral on a function source");
        sampledSql.Should().Be(plainSql, "a table sample is SQL-identity-neutral on a function source");
        hintedSql.Should().NotContain("NestLoop", "the hint must not leak into the statement text");
        ReferenceEquals(hinted, plain).Should().BeTrue("a neutral option must reuse the same plan instance, not fork the identity");
        ReferenceEquals(sampled, plain).Should().BeTrue("a neutral option must reuse the same plan instance, not fork the identity");
        QueryPlanStore.Values.Count().Should().Be(plansAfterPlain, "no neutral option may add a plan entry");
        ParametersOf(hinted).Should().Equal(new[] { "id=42" });
        ParametersOf(sampled).Should().Equal(new[] { "id=42" });

        // A source-level lock is not neutral: SQLite cannot express it on any source, so it fails closed
        // instead of silently sharing the unlocked plan.
        Action locked = () => Prepare(
            ctx,
            ctx.From(() => T320ProbeTvf.ProbeTvf(id), options => options.ForUpdate()).Select(r => r.Id),
            true);
        locked.Should().Throw<NotSupportedException>()
            .WithMessage("*can only target a physical table source*");
        QueryPlanStore.Values.Count().Should().Be(plansAfterPlain, "a refused option must not have registered a plan");
    }

    /// <summary>
    /// MX-CACHE: a captured argument that transitions <c>null</c> → value → <c>null</c> → value keeps the same
    /// plan instance and replaces the parameter value each time — no stale slot, no re-rendered statement and
    /// no sticky <c>QueryCommand.Cache</c> mutation.
    /// </summary>
    [Fact]
    public void RuntimeTvf_Cache_NullTransitions_ShouldRefreshTheParameterAndReuseThePlan()
    {
        using var ctx = SqliteTestContext.CreateSqlite();
        ctx.PurgeQueryCache();
        EntityBuilderExtensions.GetAnyCommand(ctx, ctx.From<SimpleEntity>().ToCommand()).Cache
            .Should().BeTrue("the shared Any command starts cacheable");

        string? text = null;

        QueryCommand<int> Build() => ctx.From(() => T320ProbeTvf.ProbeTvfText(text)).Select(r => r.Id);

        var first = Prepare(ctx, Build(), true);
        var firstSql = Normalize(first.DbCommand.CommandText);
        ParameterValues(first).Should().Equal(new[] { "text=<null>" }, "a captured null is a bound slot with a DBNull value");

        text = "abc";
        var second = Prepare(ctx, Build(), true);
        ParameterValues(second).Should().Equal(new[] { "text=abc" }, "the value transition replaces the parameter value");

        text = null;
        var third = Prepare(ctx, Build(), true);
        ParameterValues(third).Should().Equal(
            new[] { "text=<null>" },
            "the return transition must clear the slot again, never keep the stale 'abc'");

        text = "xyz";
        var fourth = Prepare(ctx, Build(), true);
        ParameterValues(fourth).Should().Equal(new[] { "text=xyz" }, "a later transition replaces the slot again");

        output.WriteLine(
            $"null transitions: sql=<<{firstSql}>>; run1=[{string.Join(", ", ParameterValues(first))}]; "
            + $"run2=[{string.Join(", ", ParameterValues(second))}]; run3=[{string.Join(", ", ParameterValues(third))}]; "
            + $"run4=[{string.Join(", ", ParameterValues(fourth))}]; "
            + $"refEq12={ReferenceEquals(first, second)}; refEq13={ReferenceEquals(first, third)}; "
            + $"refEq14={ReferenceEquals(first, fourth)}; planStore={QueryPlanStore.Values.Count()}; "
            + $"anyCache={ctx.AnyCommand!.Value.Cache}");

        ReferenceEquals(first, second).Should().BeTrue("the same SQL identity must reuse the cached plan");
        ReferenceEquals(first, third).Should().BeTrue("a null transition must not fork the plan identity");
        ReferenceEquals(first, fourth).Should().BeTrue("a fourth transition must stay on the same plan instance");
        Normalize(second.DbCommand.CommandText).Should().Be(firstSql, "no transition may re-render the statement");
        Normalize(third.DbCommand.CommandText).Should().Be(firstSql, "no transition may re-render the statement");
        Normalize(fourth.DbCommand.CommandText).Should().Be(firstSql, "no transition may re-render the statement");
        first.DbCommandParams.Cast<DbParameter>().Should().HaveCount(1, "the parameter slot count never changes");
        QueryPlanStore.Values.Count().Should().Be(1, "the four transitions leave exactly one plan entry");
        ctx.AnyCommand!.Value.Cache.Should().BeTrue(
            "TVF preparation must not set the sticky QueryCommand.Cache=false flag on the shared Any command");
    }
}
