using System.Data.Common;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// D199 — plan-cache safety of a scalar captured-collection <c>IN</c>/<c>Contains</c> (#199). Translating
/// a captured scalar collection in a clause whose shape is not folded into the plan key (a SELECT
/// projection, a HAVING, a JOIN ON, a nested referenced subquery, or the WHERE of the query referenced by
/// the context-shared <c>Any</c> command) must not flip the persistent <see cref="QueryCommand.Cache"/>
/// policy. The call-local cache can be bypassed via <c>storeInCache</c>, but a later reuse of a shared
/// command must keep hitting the cached plan. These tests drive a real temp-file SQLite database (the
/// placeholder <c>:memory:</c> context cannot execute) and serialize with the other process-wide
/// plan-cache tests.
/// </summary>
[Collection("DataContextCache clear")]
public class D199ScalarInPlanCacheTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Captured collections live in instance fields (not fresh per-call locals) so two builds share the
    // exact same captured member: the plan-key comparer keys a captured closure member by name, so a
    // different local name would itself change the plan key and mask the flag under test.
    private List<long> _values = [];
    private long[] _array = [];

    private static (IDataContext Ctx, string Path) CreateDb()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-d199cache-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "create table simple_entity (id integer primary key);" +
                "insert into simple_entity (id) values (1),(2),(3);" +
                "create table complex_entity (id integer primary key, nullableint integer, somestring text);" +
                "insert into complex_entity (id, nullableint, somestring) values (1, 10, 'a'),(2, null, 'b'),(3, 30, 'c');";
            cmd.ExecuteNonQuery();
        }

        var ctx = new SqliteDataContext($"Data Source={path}", new DataContextBuilder());
        ctx.EnsureConnectionOpen();
        return (ctx, path);
    }

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, true, Ct);

    private static int ParamCount<T>(DbPreparedQueryCommand<T> command)
        => command.DbCommandParams.Cast<DbParameter>().Count();

    // ------------------------------------------------------------------ scalar captured-collection builders

    private QueryCommand<long> WhereFieldScalar(EntityBuilder<IComplexEntity> e)
        => e.Where(x => _values.Contains(x.Id)).Select(x => x.Id);

    private QueryCommand<long> WhereFieldArray(EntityBuilder<IComplexEntity> e)
        => e.Where(x => _array.Contains(x.Id)).Select(x => x.Id);

    private QueryCommand<long> HavingFieldScalar(EntityBuilder<IComplexEntity> e)
        => e.GroupBy(x => x.Id).Having(x => _values.Contains(x.Id)).Select(x => x.Id);

    private QueryCommand<int> JoinFieldScalar(EntityBuilder<ISimpleEntity> simple, EntityBuilder<IComplexEntity> complex)
        => simple.Join<IComplexEntity>(complex, (s, c) => s.Id == c.Id && _values.Contains(c.Id)).Select(p => p.Item1.Id);

    private QueryCommand<bool> SelectFieldScalar(EntityBuilder<IComplexEntity> e)
        => e.Select(x => _values.Contains(x.Id));

    private QueryCommand<long> NestedHavingFieldScalar(EntityBuilder<IComplexEntity> e, IDataContext ctx)
        => e.Select(x => ctx.From<IComplexEntity>()
            .GroupBy(c => c.Id)
            .Having(c => _values.Contains(c.Id))
            .Select(c => c.Id)
            .First());

    private QueryCommand<long> NestedHavingFieldArray(EntityBuilder<IComplexEntity> e, IDataContext ctx)
        => e.Select(x => ctx.From<IComplexEntity>()
            .GroupBy(c => c.Id)
            .Having(c => _array.Contains(c.Id))
            .Select(c => c.Id)
            .First());

    // Renders a captured scalar list in an unkeyed SELECT projection: the scalar IN translator is the
    // sole writer of the sticky _dontCache flag (InValuesTranslator.cs:58-59) on the original code.
    [Fact]
    public void CapturedScalarInSelect_ShouldPreserveCommandCachePolicy()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();
            var e = ctx.From<IComplexEntity>();
            var values = new List<long> { 1, 3 };

            var command = e.Select(x => values.Contains(x.Id));
            var prepared = Prepare(ctx, command);

            prepared.DbCommand.CommandText.Should()
                .Contain(" in (", "the captured scalar list must translate into a scalar IN predicate");
            prepared.DbCommandParams.Cast<DbParameter>().Should().HaveCount(2);

            command.Cache.Should().BeTrue(
                "translating a captured scalar collection in an unkeyed SELECT must not flip the persistent command cache policy");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    // A captured scalar Contains lives in the WHERE of the query referenced by the context-shared Any
    // command. Because that condition is rendered through the shared command (whose own partitions do not
    // contain it), the translator would flip the sticky policy on the shared command. After the policy is
    // preserved, equivalent ordinary shared Any preparations must keep hitting the cached plan.
    [Fact]
    public void SharedAny_ScalarContains_ShouldPreservePolicyAndLaterCacheHits()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();
            var values = new List<long> { 1, 3 };

            var any = ctx.From<IComplexEntity>().Where(x => values.Contains(x.Id)).Any();

            any.Should().BeTrue("rows 1 and 3 exist in the seeded table");
            ctx.AnyCommand.Should().NotBeNull();

            var shared = ctx.AnyCommand!.Value;
            shared.Cache.Should().BeTrue(
                "a captured scalar Contains rendered through the shared Any command must not poison its persistent cache policy");

            var first = PrepareSharedAny(ctx);
            var second = PrepareSharedAny(ctx);

            ReferenceEquals(first, second).Should().BeTrue(
                "equivalent ordinary shared Any preparations must reuse the cached plan");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    // Mirrors the Any terminal (EntityBuilderExtensions.AnyCore) for an ordinary filter so the shared
    // command's referenced subquery is replaced exactly as a real call would, then returns the prepared
    // command instance for a reference-equality cache-hit assertion.
    private static DbPreparedQueryCommand<bool> PrepareSharedAny(IDataContext ctx)
    {
        var cmd = ctx.From<IComplexEntity>().Where(x => x.Id > 0).ToCommand();
        cmd.IgnoreColumns = true;
        var shared = EntityBuilderExtensions.GetAnyCommand(ctx, cmd);
        return (DbPreparedQueryCommand<bool>)ctx.GetPreparedQueryCommand(shared, false, true, Ct);
    }

    // The Count terminal builds its command with SelectParent (CountCore). Reusing one command instance
    // makes the plan-cache hit observable by reference, mirroring the shared Any contract: the captured
    // scalar WHERE is shape-keyed, so the policy must survive and an equivalent call must hit the cache.
    [Fact]
    public void SharedCount_ScalarContains_ShouldPreservePolicyAndLaterCacheHits()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();
            _values = [1, 3];

            var countCommand = ctx.From<IComplexEntity>()
                .Where(x => _values.Contains(x.Id))
                .SelectParent(x => SqlFunctions.Sql.count());
            countCommand.SingleRow = true;

            var first = Prepare(ctx, countCommand);
            countCommand.Cache.Should().BeTrue(
                "translating a captured scalar Contains into the count command must not flip its persistent cache policy");

            var second = Prepare(ctx, countCommand);
            ReferenceEquals(first, second).Should().BeTrue(
                "an equivalent count over the same keyed captured scalar WHERE must reuse the cached plan");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    // A captured scalar list in HAVING / JOIN ON / SELECT / a nested referenced subquery is not folded
    // into the plan key. Changing the captured contents between equivalent executions must rebuild the
    // SQL and follow the current values; the persistent command policy must stay enabled.
    [Fact]
    public void UnkeyedCapturedScalarIn_ShouldBypassCacheAndReflectChangedValues()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();
            var e = ctx.From<IComplexEntity>();
            var simple = ctx.From<ISimpleEntity>();
            var complex = ctx.From<IComplexEntity>();

            // HAVING
            _values = [1, 3];
            var havingCmd1 = HavingFieldScalar(e);
            var having1 = Prepare(ctx, havingCmd1);
            ParamCount(having1).Should().Be(2);
            ctx.ToList(having1).OrderBy(x => x).Should().Equal(1L, 3L);

            _values = [2];
            var havingCmd2 = HavingFieldScalar(e);
            var having2 = Prepare(ctx, havingCmd2);
            ParamCount(having2).Should().Be(1);
            ctx.ToList(having2).Should().Equal(2L);
            ReferenceEquals(having1, having2).Should().BeFalse(
                "a scalar IN in HAVING is not folded into the plan key");
            havingCmd2.Cache.Should().BeTrue(
                "the call-local bypass must not flip the persistent command policy");

            // JOIN ON
            _values = [1, 3];
            var join1 = Prepare(ctx, JoinFieldScalar(simple, complex));
            ctx.ToList(join1).OrderBy(x => x).Should().Equal(1, 3);

            _values = [2];
            var joinCmd2 = JoinFieldScalar(simple, complex);
            var join2 = Prepare(ctx, joinCmd2);
            ctx.ToList(join2).Should().Equal(2);
            ReferenceEquals(join1, join2).Should().BeFalse(
                "a scalar IN in a JOIN condition is not folded into the plan key");
            joinCmd2.Cache.Should().BeTrue();

            // SELECT
            _values = [1, 3];
            var select1 = Prepare(ctx, SelectFieldScalar(e));
            ctx.ToList(select1).Should().Equal(true, false, true);

            _values = [2];
            var selectCmd2 = SelectFieldScalar(e);
            var select2 = Prepare(ctx, selectCmd2);
            ctx.ToList(select2).Should().Equal(false, true, false);
            ReferenceEquals(select1, select2).Should().BeFalse(
                "a scalar IN in a SELECT column is not folded into the plan key");
            selectCmd2.Cache.Should().BeTrue();

            // nested referenced subquery: the scalar list sits in the referenced command's HAVING
            _values = [1, 3];
            var nested1 = Prepare(ctx, NestedHavingFieldScalar(e, ctx));
            ParamCount(nested1).Should().Be(2, "the referenced HAVING shape has two values");
            var nestedResult1 = ctx.ToList(nested1);
            nestedResult1.Should().HaveCount(3);
            nestedResult1.Distinct().Should().HaveCount(1);
            new long[] { 1, 3 }.Should().Contain(nestedResult1[0]);

            _values = [2];
            var nestedCmd2 = NestedHavingFieldScalar(e, ctx);
            var nested2 = Prepare(ctx, nestedCmd2);
            ParamCount(nested2).Should().Be(1, "the changed referenced HAVING shape has one value");
            ReferenceEquals(nested1, nested2).Should().BeFalse(
                "a scalar IN in a nested referenced HAVING is not folded into the owning plan key");
            ctx.ToList(nested2).Distinct().Should().Equal(2L);
            nestedCmd2.Cache.Should().BeTrue();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    // A captured scalar list / array in WHERE is shape-keyed: an equal-shape repetition must be a cache
    // hit, while a changed shape must re-key and rebuild with the current values.
    [Fact]
    public void KeyedCapturedScalarIn_ShouldReusePlanAndRefreshValues()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();
            var e = ctx.From<IComplexEntity>();

            _values = [1, 3];
            var list1 = Prepare(ctx, WhereFieldScalar(e));
            var list2 = Prepare(ctx, WhereFieldScalar(e));
            ReferenceEquals(list1, list2).Should().BeTrue(
                "a captured scalar list in WHERE is shape-keyed and cacheable");
            ctx.ToList(list2).OrderBy(x => x).Should().Equal(1L, 3L);

            _array = [2L, 3L];
            var array1 = Prepare(ctx, WhereFieldArray(e));
            var array2 = Prepare(ctx, WhereFieldArray(e));
            ReferenceEquals(array1, array2).Should().BeTrue(
                "a captured scalar array in WHERE is shape-keyed and cacheable");
            ctx.ToList(array2).OrderBy(x => x).Should().Equal(2L, 3L);

            _values = [2];
            var list3 = Prepare(ctx, WhereFieldScalar(e));
            ReferenceEquals(list2, list3).Should().BeFalse(
                "a changed captured scalar WHERE shape re-keys the plan");
            list3.DbCommand.CommandText.Replace("\r\n", "\n").Should().Contain(" in (");
            ctx.ToList(list3).Should().Equal(2L);

            _array = [2L, 3L, 99L];
            var array3 = Prepare(ctx, WhereFieldArray(e));
            ReferenceEquals(array2, array3).Should().BeFalse(
                "a changed captured scalar array shape re-keys the plan");
            ctx.ToList(array3).OrderBy(x => x).Should().Equal(2L, 3L);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    // An inline NewArrayExpression is part of the expression shape, so its values are stable for a cached
    // plan: the new scalar probe must not classify it as an unkeyed capture and must not introduce any new
    // suppression. The literal path keeps its keyed, cacheable behavior.
    [Fact]
    public void InlineScalarArray_ShouldPreserveExistingCacheBehavior()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();
            var e = ctx.From<IComplexEntity>();

            var select = e.Select(x => new long[] { 1, 3 }.Contains(x.Id));
            var select1 = Prepare(ctx, select);
            select.HasUnkeyedScalarInValues.Should().BeFalse(
                "an inline array is part of the expression shape and must not trigger call-local suppression");
            select.Cache.Should().BeTrue();
            ctx.ToList(select1).Should().Equal(true, false, true);

            var selectAgain = Prepare(ctx, e.Select(x => new long[] { 1, 3 }.Contains(x.Id)));
            ReferenceEquals(select1, selectAgain).Should().BeTrue(
                "an inline array projection stays cacheable");

            var where = e.Where(x => new long[] { 1, 3 }.Contains(x.Id)).Select(x => x.Id);
            var where1 = Prepare(ctx, where);
            where.HasUnkeyedScalarInValues.Should().BeFalse(
                "an inline array in WHERE is keyed and stays cacheable");
            where.Cache.Should().BeTrue();
            ctx.ToList(where1).OrderBy(x => x).Should().Equal(1L, 3L);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    // Prepared without hashing (storeInCache:false) the condition's value partitions are never computed,
    // so the translator evaluates the captured collection at render time. The persistent policy must
    // still survive.
    [Fact]
    public void ScalarIn_StoreInCacheFalse_ShouldNotMutatePolicy()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();
            _values = [1, 3];
            var command = WhereFieldScalar(ctx.From<IComplexEntity>());

            var prepared = ctx.GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: false, Ct);
            prepared.Should().NotBeNull();

            command.Cache.Should().BeTrue(
                "an uncached preparation must not flip the persistent command cache policy");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    // An explicitly disabled caller policy is authoritative: preparing and reusing the command must leave
    // it disabled (the fix must never set Cache = true).
    [Fact]
    public void ScalarIn_ExplicitCacheFalse_ShouldRemainFalse()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();
            _values = [1, 3];
            var command = WhereFieldScalar(ctx.From<IComplexEntity>());
            command.Cache = false;

            var first = Prepare(ctx, command);
            command.Cache.Should().BeFalse("an explicit disabled policy must survive preparation");

            var second = Prepare(ctx, command);
            command.Cache.Should().BeFalse("an explicit disabled policy must survive reuse");
            first.Should().NotBeNull();
            second.Should().NotBeNull();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    // Reset/reprepare and clone must preserve the preparation-policy semantics, and replacing the shared
    // command's referenced unkeyed query with an ordinary one must restore cache-hit capability.
    [Fact]
    public void ScalarIn_ResetAndClone_ShouldPreservePreparationPolicySemantics()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();
            var e = ctx.From<IComplexEntity>();
            _values = [1, 3];

            var command = HavingFieldScalar(e);
            Prepare(ctx, command);
            command.HasUnkeyedScalarInValues.Should().BeTrue(
                "a captured scalar list in HAVING is not folded into the plan key");
            command.Cache.Should().BeTrue();

            var clone = command.Clone();
            clone.HasUnkeyedScalarInValues.Should().BeTrue(
                "a clone copies the computed unkeyed scalar flag");
            clone.Cache.Should().BeTrue("a clone copies the persistent cache policy");

            command.ResetPreparation();
            command.HasUnkeyedScalarInValues.Should().BeFalse(
                "ResetPreparation clears the unkeyed scalar flag");
            Prepare(ctx, command);
            command.HasUnkeyedScalarInValues.Should().BeTrue(
                "re-preparation recomputes the unkeyed scalar flag from the current command graph");
            command.Cache.Should().BeTrue();

            // unkeyed shared command -> ordinary replacement restores cache-hit capability
            var unkeyed = ctx.From<IComplexEntity>().Where(x => _values.Contains(x.Id)).Any();
            unkeyed.Should().BeTrue();
            ctx.AnyCommand!.Value.HasUnkeyedScalarInValues.Should().BeTrue(
                "the shared Any command is flagged for the referenced scalar WHERE");

            var ordinaryFirst = PrepareSharedAny(ctx);
            ctx.AnyCommand.Value.HasUnkeyedScalarInValues.Should().BeFalse(
                "replacing the referenced query with an ordinary WHERE recomputes the flag");
            var ordinarySecond = PrepareSharedAny(ctx);
            ReferenceEquals(ordinaryFirst, ordinarySecond).Should().BeTrue(
                "an ordinary replacement restores the shared command's cache-hit capability");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    // The scalar value-list SqlFunctions.Sql.@in entry point (not Contains) in an unkeyed clause must be
    // classified exactly like a captured Contains: the call-local cache is bypassed and the persistent
    // command policy survives.
    [Fact]
    public void UnkeyedScalarInViaSqlFunctionsIn_ShouldPreservePolicyAndBypassCache()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();
            var e = ctx.From<IComplexEntity>();

            // SELECT projection: a scalar @in is not folded into the plan key.
            _values = [1, 3];
            var select1 = Prepare(ctx, e.Select(x => SqlFunctions.Sql.@in(x.Id, _values)));
            ParamCount(select1).Should().Be(2, "the scalar @in shape has two values");
            ctx.ToList(select1).Should().Equal(true, false, true);

            _values = [2];
            var selectCmd2 = e.Select(x => SqlFunctions.Sql.@in(x.Id, _values));
            var select2 = Prepare(ctx, selectCmd2);
            ParamCount(select2).Should().Be(1, "the changed scalar @in shape has one value");
            ctx.ToList(select2).Should().Equal(false, true, false);
            ReferenceEquals(select1, select2).Should().BeFalse(
                "a scalar @in in a SELECT column is not folded into the plan key");
            selectCmd2.Cache.Should().BeTrue(
                "the call-local bypass must not flip the persistent command policy");

            // HAVING: the same entry point is unkeyed there too.
            _values = [1, 3];
            var having1 = Prepare(ctx, e.GroupBy(x => x.Id).Having(x => SqlFunctions.Sql.@in(x.Id, _values)).Select(x => x.Id));
            ParamCount(having1).Should().Be(2);
            ctx.ToList(having1).OrderBy(x => x).Should().Equal(1L, 3L);

            _values = [2];
            var havingCmd2 = e.GroupBy(x => x.Id).Having(x => SqlFunctions.Sql.@in(x.Id, _values)).Select(x => x.Id);
            var having2 = Prepare(ctx, havingCmd2);
            ParamCount(having2).Should().Be(1);
            ctx.ToList(having2).Should().Equal(2L);
            ReferenceEquals(having1, having2).Should().BeFalse(
                "a scalar @in in HAVING is not folded into the plan key");
            havingCmd2.Cache.Should().BeTrue();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    // A captured scalar array (a field, not an inline new[] literal) in an unkeyed clause: the plan key
    // does not see the array, so changed contents must not be served from a stale plan while the policy
    // survives.
    [Fact]
    public void CapturedScalarArrayInUnkeyedClause_ShouldBypassCacheAndPreservePolicy()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();
            var e = ctx.From<IComplexEntity>();

            _array = [1L, 3L];
            var select1 = Prepare(ctx, e.Select(x => _array.Contains(x.Id)));
            ParamCount(select1).Should().Be(2);
            ctx.ToList(select1).Should().Equal(true, false, true);

            _array = [2L];
            var selectCmd2 = e.Select(x => _array.Contains(x.Id));
            var select2 = Prepare(ctx, selectCmd2);
            ParamCount(select2).Should().Be(1);
            ctx.ToList(select2).Should().Equal(false, true, false);
            ReferenceEquals(select1, select2).Should().BeFalse(
                "a captured scalar array in SELECT is not folded into the plan key");
            selectCmd2.Cache.Should().BeTrue();

            // nested referenced subquery: the array sits in the referenced command's HAVING
            _array = [1L, 3L];
            var nested1 = Prepare(ctx, NestedHavingFieldArray(e, ctx));
            ParamCount(nested1).Should().Be(2, "the referenced HAVING shape has two values");

            _array = [2L];
            var nestedCmd2 = NestedHavingFieldArray(e, ctx);
            var nested2 = Prepare(ctx, nestedCmd2);
            ParamCount(nested2).Should().Be(1, "the changed referenced HAVING shape has one value");
            ReferenceEquals(nested1, nested2).Should().BeFalse(
                "a captured scalar array in a nested referenced HAVING is not folded into the owning plan key");
            ctx.ToList(nested2).Distinct().Should().Equal(2L);
            nestedCmd2.Cache.Should().BeTrue();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    // Duplicates and a scalar default(T) (0L here) must be rendered faithfully in an unkeyed clause: the
    // extra parameters do not change results, and the policy survives.
    [Fact]
    public void UnkeyedScalarIn_DuplicatesAndScalarDefault_ShouldPreservePolicy()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();
            var e = ctx.From<IComplexEntity>();

            _values = [0L, 1L, 1L, 3L];
            var select1 = Prepare(ctx, e.Select(x => _values.Contains(x.Id)));
            ParamCount(select1).Should().Be(4, "duplicates and default(T) each materialise as a value");
            ctx.ToList(select1).Should().Equal(true, false, true);

            // A changed unkeyed shape must rebuild (0 is a value, not a null/skip marker) and keep the policy.
            _values = [0L, 2L];
            var selectCmd2 = e.Select(x => _values.Contains(x.Id));
            var select2 = Prepare(ctx, selectCmd2);
            ParamCount(select2).Should().Be(2);
            ctx.ToList(select2).Should().Equal(false, true, false);
            ReferenceEquals(select1, select2).Should().BeFalse(
                "a captured scalar list with duplicates/default in SELECT is not folded into the plan key");
            selectCmd2.Cache.Should().BeTrue(
                "duplicates/default in an unkeyed scalar collection must not mutate the cache policy");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    // The fourth storeInCache x Cache combination: an explicitly disabled persistent policy prepared
    // without caching must stay disabled after preparation and reuse (the fix must never set Cache=true).
    [Fact]
    public void ScalarIn_StoreInCacheFalseAndCacheFalse_ShouldRemainFalse()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();
            _values = [1, 3];
            var command = WhereFieldScalar(ctx.From<IComplexEntity>());
            command.Cache = false;

            var first = ctx.GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: false, Ct);
            first.Should().NotBeNull();
            command.Cache.Should().BeFalse("an explicit disabled policy must survive an uncached preparation");

            var second = ctx.GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: false, Ct);
            second.Should().NotBeNull();
            command.Cache.Should().BeFalse("an explicit disabled policy must survive reuse without caching");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }
}
