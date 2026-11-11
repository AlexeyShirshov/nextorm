using System.Data.Common;
using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.Core.Tests;

/// <summary>
/// T324 D8 — E10 (AC10) combined plan-cache regressions for correlated CROSS/OUTER APPLY after a join,
/// on the repaired tree. T316 owns the structural comparison itself
/// (<c>JoinExpressionPlanEqualityComparer</c>); this class owns only the after-join shapes:
/// <list type="bullet">
/// <item>equal structures built twice with <em>fresh</em> <see cref="ParameterExpression"/> instances must
/// share exactly one plan-cache entry (the second preparation is a cache hit), for both kinds and both
/// receiver forms the harness allows;</item>
/// <item>structurally different sources, operators, join order, slot shape/arity and the two lambda-return
/// forms must each own a distinct entry — no collision;</item>
/// <item>a reused (cache-hit) plan renders identical SQL and the second command still resolves its own
/// owner/slot/alias, with repeated CLR types proving the occurrence;</item>
/// <item>a call-local non-caching preparation never sets the sticky <see cref="QueryCommand.Cache"/> flag
/// and never disables the plan cache context-wide.</item>
/// </list>
/// Entry counts use the plan store's own enumeration (<c>QueryPlanStore.Values</c>), the convention already
/// used by the plan-cache guards. SQL is rendered by the real SQL Server planner over a placeholder
/// connection string; no connection is opened. Runs in the serialized "Query cache controls" collection
/// because the store is process-wide.
/// </summary>
[Collection("Query cache controls")]
public class T324ApplyCacheTests
{
    private static SqlServerDataContext CreateContext()
        => new(T324ApplyFixture.SqlServerConnectionString, new DataContextBuilder());

    private static int CacheEntryCount() => QueryPlanStore.Values.Count();

    private static IPreparedQueryCommand<T324FlatRow> PrepareCached(IDataContext ctx, QueryCommand<T324FlatRow> command)
        => ctx.GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: true, CancellationToken.None);

    private static string PreparedSql(IPreparedQueryCommand<T324FlatRow> prepared)
        => T324ApplyFixture.Normalize(((DbPreparedQueryCommand<T324FlatRow>)prepared).DbCommand.CommandText);

    private static ParameterExpression ApplyOwner(QueryCommand command)
        => command.Joins!.Single(j => j.ApplySource is not null).ApplySource!.Parameters[0];

    private static QueryCommand<T324FlatRow> Flat3(JoinedEntityBuilder<T324A, T324B, T324C> applied)
        => applied.Select(q => new T324FlatRow { S1 = q.Item1.Id, S2 = q.Item2.Id, S3 = q.Item3.Id });

    // ---------------------------------------------------------------------------------------------
    // AC10.1 — equal fresh-parameter structures reuse one plan (positional after-join receiver).
    // ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(JoinType.CrossApply)]
    [InlineData(JoinType.OuterApply)]
    public void Equal_fresh_parameter_structures_after_a_join_share_one_plan_entry(JoinType kind)
    {
        using var ctx = CreateContext();
        ctx.PurgeQueryCache();
        CacheEntryCount().Should().Be(0, "the purge must leave a clean plan cache for the counting assertions");

        var first = PositionalAfterJoin(ctx, kind);
        var second = PositionalAfterJoin(ctx, kind);

        ApplyOwner(first).Should().NotBeSameAs(
            ApplyOwner(second), "each build evaluates its own lambda literal, so the parameters are fresh instances");
        ApplyOwner(first).Name.Should().Be(ApplyOwner(second).Name);

        var firstPrepared = PrepareCached(ctx, first);
        CacheEntryCount().Should().Be(1, "the first preparation stores exactly one plan for the shape");

        var secondPrepared = PrepareCached(ctx, second);
        CacheEntryCount().Should().Be(
            1, $"an equal {kind} structure built with fresh parameters must reuse the stored plan, not add a second entry");
        secondPrepared.Should().BeSameAs(firstPrepared, "the second preparation must be served by the stored plan");

        var sql = PreparedSql(secondPrepared);
        sql.Should().Be(PreparedSql(firstPrepared), "the shared plan renders one and the same SQL");
        sql.Should().Contain("where t3.Id = t2.Code", "the joined occurrence (slot 2) is the correlation target");
        sql.Should().Contain(kind == JoinType.CrossApply ? "cross apply" : "outer apply");
    }

    // ---------------------------------------------------------------------------------------------
    // AC10.1 — equal fresh-parameter structures reuse one plan (generated-alias receiver form).
    // ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(JoinType.CrossApply)]
    [InlineData(JoinType.OuterApply)]
    public void Equal_fresh_parameter_structures_on_an_alias_receiver_share_one_plan_entry(JoinType kind)
    {
        using var ctx = CreateContext();
        ctx.PurgeQueryCache();
        CacheEntryCount().Should().Be(0);

        var first = AliasAfterJoin(ctx, kind);
        var second = AliasAfterJoin(ctx, kind);

        ApplyOwner(first).Should().NotBeSameAs(ApplyOwner(second));

        var firstPrepared = PrepareCached(ctx, first);
        CacheEntryCount().Should().Be(1);

        var secondPrepared = PrepareCached(ctx, second);
        CacheEntryCount().Should().Be(1, $"the equal {kind} alias-receiver structure must reuse the stored plan");
        secondPrepared.Should().BeSameAs(firstPrepared);

        var sql = PreparedSql(secondPrepared);
        sql.Should().Be(PreparedSql(firstPrepared));
        sql.Should().Contain("where t3.Id = t2.Code", "the named slot [JoinSlot(2)] resolves the joined occurrence");
        sql.Should().Contain(kind == JoinType.CrossApply ? "cross apply" : "outer apply");
    }

    // ---------------------------------------------------------------------------------------------
    // AC10.2 — structurally different shapes never share an entry.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Distinct_structures_do_not_collide_in_the_plan_cache()
    {
        using var ctx = CreateContext();
        ctx.PurgeQueryCache();
        CacheEntryCount().Should().Be(0);

        var shapes = DistinctShapes();
        var prepared = new List<(string Name, IPreparedQueryCommand<T324FlatRow> Command)>();

        foreach (var (name, build) in shapes)
        {
            var before = CacheEntryCount();
            var command = PrepareCached(ctx, build(ctx));
            CacheEntryCount().Should().Be(
                before + 1, $"the distinct structure '{name}' must own its own plan-cache entry (no collision)");
            prepared.Add((name, command));
        }

        CacheEntryCount().Should().Be(prepared.Count, "the entry count equals the number of distinct shapes");

        // Second pass: every shape still resolves to the very plan it created, and the cache does not grow.
        foreach (var (name, command) in prepared)
        {
            var again = PrepareCached(ctx, DistinctShapes().Single(s => s.Name == name).Build(ctx));
            again.Should().BeSameAs(command, $"the stored plan for '{name}' must be reachable by an equal fresh build");
            CacheEntryCount().Should().Be(prepared.Count, "a cache hit must not add an entry");
        }

        // The two lambda-return forms are the sharpest non-collision case: identical body logic, different
        // expression shape. They must own separate entries even if their rendered SQL coincides.
        var entityForm = prepared.Single(p => p.Name == "apply-source/EntityBuilder-return").Command;
        var queryForm = prepared.Single(p => p.Name == "apply-source/QueryCommand-return").Command;
        queryForm.Should().NotBeSameAs(entityForm);
        PreparedSql(queryForm).Should().Contain("where t3.Id = t2.Code");
        PreparedSql(entityForm).Should().Contain("where t3.Id = t2.Code");
    }

    // ---------------------------------------------------------------------------------------------
    // AC10.3 — a reused plan restores the exact owner/slot/alias metadata (repeated CLR types).
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_reused_plan_restores_the_exact_owner_slot_and_alias()
    {
        using var ctx = CreateContext();
        ctx.PurgeQueryCache();
        CacheEntryCount().Should().Be(0);

        // FROM and JOIN are the same CLR type (and the same table), so only the occurrence proves the
        // binding: slot 1 is the FROM source, slot 2 the joined source.
        var first = SameTypeAfterJoin(ctx);
        var second = SameTypeAfterJoin(ctx);

        var firstOwner = ApplyOwner(first);
        var secondOwner = ApplyOwner(second);
        firstOwner.Should().NotBeSameAs(secondOwner, "the second command owns its own fresh parameter instance");

        var firstPrepared = PrepareCached(ctx, first);
        CacheEntryCount().Should().Be(1);

        var secondPrepared = PrepareCached(ctx, second);
        CacheEntryCount().Should().Be(1, "the second preparation is a cache hit");
        secondPrepared.Should().BeSameAs(firstPrepared);

        var cachedSql = PreparedSql(secondPrepared);
        cachedSql.Should().Contain("where t3.Id = t2.Code", "the reused plan must target the joined occurrence");
        cachedSql.Should().NotContain("where t3.Id = t1.Code", "no stale binding to the FROM occurrence");
        cachedSql.Should().NotContain("= Code", "no unqualified column may survive the reuse");

        // The cache-hit command's binding metadata must stay self-consistent with the join tree the
        // command actually holds — whichever route served the hit: the ordinary normalized lookup keeps
        // the command's own tree, while the shared raw-preparation reuse adopts the stored join tree. A
        // binding that named a foreign parameter instance would be a stale binding.
        second.IsPrepared.Should().BeTrue("a cache lookup happens after the second command is prepared or adopts a preparation");
        var binding = second.GetOuterReferenceBinding(0);
        binding.Should().NotBeNull("the second command keeps an occurrence-bound outer reference");
        binding!.Value.Owner.Should().BeSameAs(
            ApplyOwner(second),
            "the bound owner must be the APPLY source parameter the command actually holds — never a foreign instance");

        // A third, independent build proves the occurrence is a property of the shape, not of the stored
        // command: it renders the same SQL and is served by the same entry.
        var third = SameTypeAfterJoin(ctx);
        ApplyOwner(third).Should().NotBeSameAs(firstOwner);
        PreparedSql(PrepareCached(ctx, third)).Should().Be(cachedSql, "a fresh equal build resolves the same occurrence");
        CacheEntryCount().Should().Be(1, "the third build is another cache hit");
    }

    // ---------------------------------------------------------------------------------------------
    // AC10.4 (negative) — no sticky Cache mutation and no cache-wide disablement.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_non_caching_correlated_apply_preparation_does_not_disable_the_plan_cache()
    {
        using var ctx = CreateContext();

        // The context-shared Any command is the exact object the sticky _dontCache trap would disable for
        // the whole context; it is created (prepared) here without executing anything.
        var sharedAny = EntityBuilderExtensions.GetAnyCommand(ctx, ctx.From<T324A>().Where(a => a.Id > 0).ToCommand());
        sharedAny.Cache.Should().BeTrue("the shared Any command starts cacheable");

        ctx.PurgeQueryCache();
        CacheEntryCount().Should().Be(0);

        var command = PositionalAfterJoin(ctx, JoinType.CrossApply);

        // Call-local non-caching preparation: nothing may be stored, and no flag may be mutated.
        ctx.GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: false, CancellationToken.None);
        CacheEntryCount().Should().Be(0, "a call-local non-caching preparation must not insert a plan entry");
        command.Cache.Should().BeTrue("preparation must never set the sticky _dontCache flag on the command");
        sharedAny.Cache.Should().BeTrue("the shared command's cache flag must be unchanged by the APPLY preparation");
        EntityBuilderExtensions.GetAnyCommand(ctx, ctx.From<T324A>().Where(a => a.Id > 0).ToCommand())
            .Should().BeSameAs(sharedAny, "the context still owns the same shared command instance");
        ctx.QueryCacheEnabled.Should().BeTrue("no cache-wide disablement");

        // The cache is still usable afterwards: a fresh, equal build prepared with caching stores exactly
        // one plan, and the next equal build is served by it.
        var firstPrepared = PrepareCached(ctx, PositionalAfterJoin(ctx, JoinType.CrossApply));
        CacheEntryCount().Should().Be(1, "an equal fresh build prepared with caching stores one plan");

        var hit = PrepareCached(ctx, PositionalAfterJoin(ctx, JoinType.CrossApply));
        CacheEntryCount().Should().Be(1, "the cache stayed usable after the non-caching preparation");
        hit.Should().BeSameAs(firstPrepared);

        // A later, unrelated query on the same context still caches normally.
        var later = ctx.From<T324D>().Where(d => d.Id > 0).Select(d => d.Id);
        var laterPrepared = PrepareCachedLater(ctx, later);
        CacheEntryCount().Should().Be(2);
        var laterAgain = PrepareCachedLater(ctx, ctx.From<T324D>().Where(d => d.Id > 0).Select(d => d.Id));
        CacheEntryCount().Should().Be(2, "the unrelated query caches and reuses its plan");
        laterAgain.Should().BeSameAs(laterPrepared);
        later.Cache.Should().BeTrue();
    }

    private static IPreparedQueryCommand<int> PrepareCachedLater(IDataContext ctx, QueryCommand<int> command)
        => ctx.GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: true, CancellationToken.None);

    // ---------------------------------------------------------------------------------------------
    // #343 (T343) C5 — a joined runtime-factory TVF apply keeps the flat-path cache semantics.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// A joined receiver applied to a runtime table-valued-function factory (#343). The factory's first
    /// argument is the correlated column of slot 1, the second a captured value, so a stale plan shows up
    /// as the wrong bound parameter.
    /// </summary>
    private static QueryCommand<T324FlatRow> JoinWithRuntimeTvfApply(IDataContext ctx, int capture)
        => ctx.From<T320MatrixRow>()
            .Join(ctx.From<T320MatrixRow>(), (first, second) => first.Id == second.Id)
            .CrossApply((Projection<T320MatrixRow, T320MatrixRow> p) => T320MatrixTvf.EchoPair(p.Item1.Id, capture))
            .Select(q => new T324FlatRow { S1 = q.Item1.Id, S2 = q.Item2.Id, S3 = q.Item3.Id });

    private static DbPreparedQueryCommand<T324FlatRow> PrepareTvfCached(IDataContext ctx, QueryCommand<T324FlatRow> command)
        => (DbPreparedQueryCommand<T324FlatRow>)PrepareCached(ctx, command);

    private static List<string> ParametersOf<T>(DbPreparedQueryCommand<T> prepared)
        => [.. prepared.DbCommandParams.Cast<DbParameter>().Select(p => $"{p.ParameterName}={p.Value ?? "null"}")];

    [Fact]
    public void A_joined_runtime_factory_apply_shares_one_plan_and_refreshes_a_changed_capture()
    {
        using var ctx = CreateContext();
        ctx.PurgeQueryCache();
        CacheEntryCount().Should().Be(0, "the purge must leave a clean plan cache for the counting assertions");

        const int capture = 3;

        // Two structurally equal builds: every call evaluates its own lambda literal, so both the APPLY
        // outer parameter and the closure are fresh instances.
        var firstPrepared = PrepareTvfCached(ctx, JoinWithRuntimeTvfApply(ctx, capture));
        CacheEntryCount().Should().Be(1, "the first preparation stores exactly one plan for the shape");
        ParametersOf(firstPrepared).Should().Equal(new[] { "capture=3" }, "the capture travels as one bound parameter");

        var secondPrepared = PrepareTvfCached(ctx, JoinWithRuntimeTvfApply(ctx, capture));
        CacheEntryCount().Should().Be(1, "an equal joined + runtime-TVF apply structure must reuse the stored plan");
        secondPrepared.Should().BeSameAs(firstPrepared, "structurally equal joined+TVF apply commands share the prepared command");

        var sql = T324ApplyFixture.Normalize(secondPrepared.DbCommand.CommandText);
        sql.Should().Contain("cross apply ", "the runtime factory is applied through the flat transition");
        sql.Should().Contain("echo_rows_pair", "the factory call stays the applied source");
        sql.Should().MatchRegex(@"echo_rows_pair\([^)]*t1\.Id", "the correlated argument addresses slot 1, this command's FROM source");
        sql.Should().NotMatchRegex(@"echo_rows_pair\([^)]*t2\.Id", "slot 2 is the joined occurrence and is not the addressed left row");

        // A changed captured argument keeps the same shape (so the same plan) but must bind the new value:
        // returning the previous one would be the stale correlation the regression is about.
        var changedPrepared = PrepareTvfCached(ctx, JoinWithRuntimeTvfApply(ctx, 11));
        changedPrepared.Should().BeSameAs(firstPrepared, "the capture is a parameter, so the shape still shares one plan");
        CacheEntryCount().Should().Be(1, "a capture change must not add a plan entry");
        ParametersOf(changedPrepared).Should().Equal(new[] { "capture=11" }, "the cache hit binds the new capture, never the previous 3");
        T324ApplyFixture.Normalize(changedPrepared.DbCommand.CommandText).Should().Be(sql, "a capture change must not re-render the statement");
    }
    // ---------------------------------------------------------------------------------------------
    // Builders — every call evaluates a fresh lambda literal, so every parameter instance is fresh.
    // ---------------------------------------------------------------------------------------------

    private static QueryCommand<T324FlatRow> PositionalAfterJoin(IDataContext ctx, JoinType kind)
    {
        var joined = ctx.From<T324A>().Join(ctx.From<T324B>(), (a, b) => a.Id == b.Id);
        var applied = kind == JoinType.CrossApply
            ? joined.CrossApply((Projection<T324A, T324B> p) => ctx.From<T324C>().Where(c => c.Id == p.Item2.Code))
            : joined.OuterApply((Projection<T324A, T324B> p) => ctx.From<T324C>().Where(c => c.Id == p.Item2.Code));

        return Flat3(applied);
    }

    private static QueryCommand<T324FlatRow> AliasAfterJoin(IDataContext ctx, JoinType kind)
    {
        var aliased = ctx.From<T324A>().JoinAlias<EntityBuilder<CacheAliasOf<T324A, T324B>>, CacheAliasOf<T324A, T324B>, T324B>(
            static dc => new EntityBuilder<CacheAliasOf<T324A, T324B>>(dc),
            (a, b) => a.Id == b.Id);

        var applied = aliased.JoinAlias<EntityBuilder<CacheAliasOf3<T324A, T324B, T324C>>, CacheAliasOf3<T324A, T324B, T324C>, T324C>(
            static dc => new EntityBuilder<CacheAliasOf3<T324A, T324B, T324C>>(dc),
            p => ctx.From<T324C>().Where(c => c.Id == p.Buyer3.Code),
            kind);

        return applied.Select(q => new T324FlatRow { S1 = q.Item1.Id, S2 = q.Buyer3.Code, S3 = q.Third.Id });
    }

    private static QueryCommand<T324FlatRow> SameTypeAfterJoin(IDataContext ctx)
        => ctx.From<T324A>()
            .Join(ctx.From<T324A>(), (a, b) => a.Id == b.Id)
            .CrossApply((Projection<T324A, T324A> p) => ctx.From<T324C>().Where(c => c.Id == p.Item2.Code))
            .Select(q => new T324FlatRow { S1 = q.Item1.Id, S2 = q.Item2.Id, S3 = q.Item3.Id });

    private static List<(string Name, Func<IDataContext, QueryCommand<T324FlatRow>> Build)> DistinctShapes() =>
    [
        ("apply-source/EntityBuilder-return", ctx => Flat3(ctx.From<T324A>()
            .Join(ctx.From<T324B>(), (a, b) => a.Id == b.Id)
            .CrossApply((Projection<T324A, T324B> p) => ctx.From<T324C>().Where(c => c.Id == p.Item2.Code)))),

        ("apply-source/other-body", ctx => Flat3(ctx.From<T324A>()
            .Join(ctx.From<T324B>(), (a, b) => a.Id == b.Id)
            .CrossApply((Projection<T324A, T324B> p) => ctx.From<T324C>().Where(c => c.Code == p.Item2.Id)))),

        ("operator/outer-apply", ctx => Flat3(ctx.From<T324A>()
            .Join(ctx.From<T324B>(), (a, b) => a.Id == b.Id)
            .OuterApply((Projection<T324A, T324B> p) => ctx.From<T324C>().Where(c => c.Id == p.Item2.Code)))),

        ("slot-shape/from-slot", ctx => Flat3(ctx.From<T324A>()
            .Join(ctx.From<T324B>(), (a, b) => a.Id == b.Id)
            .CrossApply((Projection<T324A, T324B> p) => ctx.From<T324C>().Where(c => c.Id == p.Item1.Code)))),

        ("join-order/reversed", ctx => ctx.From<T324B>()
            .Join(ctx.From<T324A>(), (b, a) => b.Id == a.Id)
            .CrossApply((Projection<T324B, T324A> p) => ctx.From<T324C>().Where(c => c.Id == p.Item1.Code))
            .Select(q => new T324FlatRow { S1 = q.Item1.Id, S2 = q.Item2.Id, S3 = q.Item3.Id })),

        ("slot-arity/three-source-prefix", ctx => ctx.From<T324A>()
            .Join(ctx.From<T324B>(), (a, b) => a.Id == b.Id)
            .Join(ctx.From<T324D>(), (p, d) => p.Item2.Id == d.Id)
            .CrossApply((Projection<T324A, T324B, T324D> p) => ctx.From<T324C>().Where(c => c.Id == p.Item3.Code))
            .Select(q => new T324FlatRow { S1 = q.Item1.Id, S2 = q.Item2.Id, S3 = q.Item4.Id })),

        ("apply-source/QueryCommand-return", ctx => Flat3(ctx.From<T324A>()
            .Join(ctx.From<T324B>(), (a, b) => a.Id == b.Id)
            .CrossApply((Projection<T324A, T324B> p) => ctx.From<T324C>().Where(c => c.Id == p.Item2.Code).ToCommand()))),

        ("receiver/alias-form", ctx => AliasAfterJoin(ctx, JoinType.CrossApply)),
    ];

    /// <summary>
    /// Hand-written stand-in for the generated <c>AliasJoin_*</c> projection over two slots: the source
    /// generator is not attached to this project, so the alias surface is declared by hand. The joined
    /// slot is named <c>Buyer3</c> so its trailing digit can never be read as the position.
    /// </summary>
    private sealed class CacheAliasOf<T1, T2> : Projection<T1, T2>
    {
        [JoinSlot(2)]
        public T2 Buyer3 => throw new NotSupportedException();
    }

    /// <summary>Three-slot alias projection receiving the APPLY: slots 1-2 are the left prefix, 3 the APPLY.</summary>
    private sealed class CacheAliasOf3<T1, T2, T3> : Projection<T1, T2, T3>
    {
        [JoinSlot(2)]
        public T2 Buyer3 => throw new NotSupportedException();

        [JoinSlot(3)]
        public T3 Third => throw new NotSupportedException();
    }
}
