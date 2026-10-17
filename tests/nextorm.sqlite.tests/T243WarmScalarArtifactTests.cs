using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// Correctness safeguards for the T243/D243 warm scalar path (<c>Count</c>/<c>LongCount</c>/member
/// <c>Sum</c> executed from a context-owned immutable artifact) on SQLite — a real database, because
/// the path executes the cached plan and must recompute the value on every call.
/// <para>
/// Every test observes the path through behaviour, never through private state: a warm call is a call
/// that leaves the planner's pre-preparation diagnostics untouched (<see cref="QueryPlanner"/> counts a
/// publish/reuse/rejection for every fresh command it prepares), so "the fast path ran" and "the
/// ordinary path ran" are distinguishable without reflecting on the artifact store. The value itself is
/// always asserted, so a test that proves the fast path is used also proves the value is right.
/// </para>
/// </summary>
public sealed class T243WarmScalarArtifactTests
{
    private const string Schema = """
        create table d243_scalar_a (id integer primary key, amount integer null, other integer not null);
        create table d243_scalar_b (id integer primary key, amount integer null, other integer not null);
        create table d243_scalar_c (id integer primary key, amount integer null, other integer not null);
        create table d243_scalar_d (id integer primary key, amount integer null, other integer not null);
        create table d243_scalar_empty (id integer primary key, amount integer null);
        create table d243_scalar_nulls (id integer primary key, amount integer null);
        create table d243_filter_rows (id integer primary key, amount integer null);
        create table d243_tenant_rows (id integer primary key, tenant_id integer not null);
        create table d243_parent (id integer primary key, name text);
        create table d243_child (id integer primary key, parent_id integer, name text);
        insert into d243_scalar_a (id, amount, other) values (1, 10, 100), (2, 20, 200), (3, null, 300);
        insert into d243_scalar_b (id, amount, other) values (1, 1, 1), (2, 2, 2);
        insert into d243_scalar_c (id, amount, other) values (1, 1, 1), (2, 2, 2);
        insert into d243_scalar_d (id, amount, other) values (1, 1, 1), (2, 2, 2);
        insert into d243_scalar_nulls (id, amount) values (1, null), (2, null);
        insert into d243_filter_rows (id, amount) values (1, 10), (2, 20), (3, null);
        insert into d243_tenant_rows (id, tenant_id) values (1, 1), (2, 1), (3, 2), (4, 2);
        insert into d243_parent (id, name) values (1, 'p1'), (2, 'p2'), (3, 'p3');
        insert into d243_child (id, parent_id, name) values (10, 1, 'c1'), (11, 1, 'c2'), (12, 2, 'c3');
        """;

    private const string TenantKey = "d243_tenant";

    // ------------------------------------------------------------------ Count / LongCount

    [Fact]
    public void Count_ShouldReturnInt_Repeat_AndRecomputeAfterDataChange()
    {
        var (db, path) = CreateDb();
        try
        {
            // Parameterless sync terminals on a fresh fluent query: `int` and `long` results.
            db.From<ScalarRowA>().Count().Should().Be(3);
            db.From<ScalarRowA>().LongCount().Should().Be(3L);

            // Repeated calls on one context, and a fluent query rebuilt every time, stay correct.
            var rows = db.From<ScalarRowA>();
            rows.Count().Should().Be(3);
            rows.Count().Should().Be(3);
            db.From<ScalarRowA>().Count().Should().Be(3);
            db.From<ScalarRowA>().LongCount().Should().Be(3L);

            // Warm shape: five more calls prepare nothing at all.
            ResetPlannerDiagnostics();
            for (var i = 0; i < 5; i++)
                db.From<ScalarRowA>().Count().Should().Be(3);
            PlannerDiagnostics().Should().Be(0, "a warm supported scalar shape executes the cached plan");
        }
        finally { db.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Count_ShouldRecomputeAfterDataChange()
    {
        var (db, path) = CreateDb();
        try
        {
            db.From<ScalarRowA>().Count().Should().Be(3);
            db.From<ScalarRowA>().Sum(x => x.Id).Should().Be(6L);

            Execute(db, "insert into d243_scalar_a (id, amount, other) values (4, 40, 400);");

            // No cached result: the fast path executes the plan, so the new row is counted.
            db.From<ScalarRowA>().Count().Should().Be(4);
            db.From<ScalarRowA>().LongCount().Should().Be(4L);
            db.From<ScalarRowA>().Sum(x => x.Id).Should().Be(10L);

            ResetPlannerDiagnostics();
            db.From<ScalarRowA>().Count().Should().Be(4);
            PlannerDiagnostics().Should().Be(0, "the shape is warm again after the data change");
        }
        finally { db.Dispose(); File.Delete(path); }
    }

    // ------------------------------------------------------------------ Sum

    [Fact]
    public void Sum_InlineAndCachedLambda_ShouldAgreeAndShareOneArtifact()
    {
        var (db, path) = CreateDb();
        try
        {
            Expression<Func<ScalarRowA, long>> cached = x => x.Id;

            // A structurally identical but distinct tree: same parameter-rooted member read, no shared
            // instance. The artifact key holds the MemberInfo, never the expression instance.
            var parameter = Expression.Parameter(typeof(ScalarRowA), "row");
            var fresh = Expression.Lambda<Func<ScalarRowA, long>>(
                Expression.Property(parameter, nameof(ScalarRowA.Id)),
                parameter);
            fresh.Should().NotBeSameAs(cached);

            db.From<ScalarRowA>().Sum(fresh).Should().Be(6L);

            ResetPlannerDiagnostics();
            db.From<ScalarRowA>().Sum(cached).Should().Be(6L);
            PlannerDiagnostics().Should().Be(0, "an equivalent cached lambda hits the artifact the fresh tree warmed");

            ResetPlannerDiagnostics();
            db.From<ScalarRowA>().Sum(fresh).Should().Be(6L);
            PlannerDiagnostics().Should().Be(0, "the fresh tree hits the same artifact");
        }
        finally { db.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Sum_ShouldSupportNullableAndNonNullableMembers()
    {
        var (db, path) = CreateDb();
        try
        {
            // Non-nullable member.
            db.From<ScalarRowA>().Sum(x => x.Id).Should().Be(6L);
            db.From<ScalarRowA>().Sum(x => x.Other).Should().Be(600L);

            // Nullable member over a mixed set: SQL ignores the NULL row.
            db.From<ScalarRowA>().Sum(x => x.Amount).Should().Be(30);
            ResetPlannerDiagnostics();
            db.From<ScalarRowA>().Sum(x => x.Amount).Should().Be(30);
            PlannerDiagnostics().Should().Be(0, "the nullable member is a supported shape too");

            // Nullable member over an all-NULL set: the SQL aggregate is NULL, so is the result.
            db.From<NullRow>().Sum(x => x.Amount).Should().BeNull();
            ResetPlannerDiagnostics();
            db.From<NullRow>().Sum(x => x.Amount).Should().BeNull();
            PlannerDiagnostics().Should().Be(0);
        }
        finally { db.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Sum_OnEmptySet_ShouldReturnDefaults()
    {
        var (db, path) = CreateDb();
        try
        {
            db.From<EmptyRow>().Count().Should().Be(0);
            db.From<EmptyRow>().LongCount().Should().Be(0L);
            db.From<EmptyRow>().Sum(x => x.Id).Should().Be(0L);
            db.From<EmptyRow>().Sum(x => x.Amount).Should().BeNull();

            // Second round: the empty-set shape is warm and still returns the defaults.
            ResetPlannerDiagnostics();
            db.From<EmptyRow>().Count().Should().Be(0);
            db.From<EmptyRow>().LongCount().Should().Be(0L);
            db.From<EmptyRow>().Sum(x => x.Id).Should().Be(0L);
            db.From<EmptyRow>().Sum(x => x.Amount).Should().BeNull();
            PlannerDiagnostics().Should().Be(0);
        }
        finally { db.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Sum_WithCapturedClosure_ShouldBeFreshAndFallBack()
    {
        var (db, path) = CreateDb();
        try
        {
            var offset = 1L;
            db.From<ScalarRowA>().Sum(x => x.Id + offset).Should().Be(9L);

            // The closure changed between calls: the value must follow it (no stale artifact).
            offset = 10L;
            db.From<ScalarRowA>().Sum(x => x.Id + offset).Should().Be(36L);

            ResetPlannerDiagnostics();
            db.From<ScalarRowA>().Sum(x => x.Id + offset).Should().Be(36L);
            PlannerDiagnostics().Should().BeGreaterThan(0, "a captured closure is not a supported shape");

            // Composite/conditional selector: correct result, ordinary path.
            ResetPlannerDiagnostics();
            db.From<ScalarRowA>().Sum(x => x.Id > 1 ? x.Id : 0L).Should().Be(5L);
            PlannerDiagnostics().Should().BeGreaterThan(0, "a conditional body is not a supported shape");
        }
        finally { db.Dispose(); File.Delete(path); }
    }

    // ------------------------------------------------------------------ filters

    [Fact]
    public void QueryFilter_ShouldApply_AndNeverBeBypassed()
    {
        var (db, path) = CreateDb();
        try
        {
            _ = db.From<FilterRow>(cfg => cfg.HasQueryFilter(x => x.Amount != null));

            db.From<FilterRow>().Count().Should().Be(2);
            db.From<FilterRow>().LongCount().Should().Be(2L);
            db.From<FilterRow>().Sum(x => x.Id).Should().Be(3L);

            // Warm calls keep observing the filter (the guard re-resolves it on every call).
            db.From<FilterRow>().Count().Should().Be(2);
            db.From<FilterRow>().Sum(x => x.Id).Should().Be(3L);

            // IgnoreFilters keeps its own meaning: the unfiltered statement, not the warm filtered one.
            db.From<FilterRow>().IgnoreFilters().Count().Should().Be(3);
            db.From<FilterRow>().IgnoreFilters().Sum(x => x.Id).Should().Be(6L);
        }
        finally { db.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void ContextDependentFilter_ShouldNotLeakAcrossContexts()
    {
        var (dbA, pathA) = CreateDb();
        var (dbB, pathB) = CreateDb();
        try
        {
            // The filter reads the executing context, so both contexts share the plan but not the value.
            _ = dbA.From<TenantRow>(cfg => cfg.HasQueryFilter((e, c) => e.TenantId == (int)c.Properties[TenantKey]));
            dbA.Properties[TenantKey] = 1;
            dbB.Properties[TenantKey] = 2;

            dbA.From<TenantRow>().Count().Should().Be(2);
            dbB.From<TenantRow>().Count().Should().Be(2);
            dbA.From<TenantRow>().Sum(x => x.Id).Should().Be(3L);
            dbB.From<TenantRow>().Sum(x => x.Id).Should().Be(7L);

            // Repeated calls keep resolving the filter against the calling context.
            dbA.From<TenantRow>().Count().Should().Be(2);
            dbA.From<TenantRow>().Sum(x => x.Id).Should().Be(3L);
            dbB.From<TenantRow>().Sum(x => x.Id).Should().Be(7L);
        }
        finally
        {
            dbA.Dispose();
            dbB.Dispose();
            File.Delete(pathA);
            File.Delete(pathB);
        }
    }

    // ------------------------------------------------------------------ cache controls

    [Fact]
    public void QueryCacheDisabled_ShouldFallBack_AndLeaveNoStickyState()
    {
        var (db, path) = CreateDb();
        try
        {
            db.QueryCacheEnabled = false;
            db.From<ScalarRowA>().Count().Should().Be(3);
            db.From<ScalarRowA>().Sum(x => x.Id).Should().Be(6L);
            db.From<ScalarRowA>().Count().Should().Be(3);

            db.QueryCacheEnabled = true;
            db.From<ScalarRowA>().Count().Should().Be(3);
            db.From<ScalarRowA>().Count().Should().Be(3);

            ResetPlannerDiagnostics();
            db.From<ScalarRowA>().Count().Should().Be(3);
            PlannerDiagnostics().Should().Be(0, "the cacheable call re-warmed the shape after the disabled window");
        }
        finally { db.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void DisablingCacheAfterWarm_ShouldBypassArtifact_AndRestoreFastPath()
    {
        var (db, path) = CreateDb();
        try
        {
            // Warm the supported shape while the cache is enabled: the first call prepares and publishes
            // the artifact, every later call executes the warm plan.
            db.From<ScalarRowA>().Count().Should().Be(3);
            db.From<ScalarRowA>().Count().Should().Be(3);

            var prepared = new PreparedCommandCounter();
            db.AddInterceptor(prepared);

            // Precondition: the shape really is warm, i.e. the call below builds no command at all.
            prepared.Reset();
            db.From<ScalarRowA>().Count().Should().Be(3);
            prepared.Count.Should().Be(0, "the warmed shape executes from the artifact and prepares nothing");

            // `QueryCacheEnabled` is a live switch: disabling it after the warm-up must stop the artifact
            // from serving the terminal. The ordinary prepared path builds a command for each uncached
            // call, so `CommandInitialized` is the public signal that the artifact was NOT used.
            db.QueryCacheEnabled = false;
            prepared.Reset();
            db.From<ScalarRowA>().Count().Should().Be(3, "the value is still recomputed correctly");
            prepared.Count.Should().Be(1, "a disabled context prepares the command instead of executing the warm artifact");

            db.From<ScalarRowA>().Count().Should().Be(3);
            prepared.Count.Should().Be(2, "nothing is cached while the cache is disabled, so every call prepares");

            // Re-enabling restores the fast path: the plan warmed before the disabled window is still owned
            // by the store and the artifact serves the terminal again with no preparation.
            db.QueryCacheEnabled = true;
            prepared.Reset();
            ResetPlannerDiagnostics();
            db.From<ScalarRowA>().Count().Should().Be(3);
            prepared.Count.Should().Be(0, "the re-enabled context executes the warm artifact again");
            PlannerDiagnostics().Should().Be(0, "the re-enabled context did not rebuild the warm shape");

            // No sticky state: the toggle did not permanently disable the cache for later calls.
            db.From<ScalarRowA>().Count().Should().Be(3);
            db.From<ScalarRowA>().Sum(x => x.Id).Should().Be(6L);
            prepared.Reset();
            db.From<ScalarRowA>().Sum(x => x.Id).Should().Be(6L);
            prepared.Count.Should().Be(0, "the shapes warmed after the toggle keep the fast path");
        }
        finally { db.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void CacheDisabledCommand_ShouldNotPublishArtifact()
    {
        var (db, path) = CreateDb();
        try
        {
            // `QueryCommand.Cache = false` cannot be reached through the fluent terminal today: the
            // terminal builds its own command. The publish guard is therefore exercised at its own
            // seam, with the same internal API the terminal uses (no reflection, no private state).
            var command = db.From<ScalarRowA>().SelectParent(e => SqlFunctions.Sql.count());
            command.SingleRow = true;
            command.Cache = false;
            command.ExecuteScalar().Should().Be(3);

            var key = new ScalarArtifactKey(typeof(ScalarRowA), typeof(int), ScalarTerminal.Count, null);
            db.PublishScalarArtifact(key, command);

            db.TryExecuteScalarArtifact<int>(key, ReadOnlySpan<object?>.Empty, out _)
                .Should().BeFalse("a command that was never stored in the plan cache publishes no artifact");

            // The ordinary path still works and re-warms a cacheable command.
            db.From<ScalarRowA>().Count().Should().Be(3);
            db.From<ScalarRowA>().Count().Should().Be(3);
        }
        finally { db.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void PurgeQueryCache_ShouldDropArtifacts_AndRebuild()
    {
        var (db, path) = CreateDb();
        try
        {
            db.From<ScalarRowA>().Count().Should().Be(3);
            db.From<ScalarRowA>().Count().Should().Be(3);

            ResetPlannerDiagnostics();
            db.From<ScalarRowA>().Count().Should().Be(3);
            PlannerDiagnostics().Should().Be(0);

            db.PurgeQueryCache();

            ResetPlannerDiagnostics();
            db.From<ScalarRowA>().Count().Should().Be(3, "the result is recomputed after the purge");
            PlannerDiagnostics().Should().BeGreaterThan(0, "the purged shape is rebuilt from scratch");

            ResetPlannerDiagnostics();
            db.From<ScalarRowA>().Count().Should().Be(3);
            PlannerDiagnostics().Should().Be(0, "the rebuilt shape is warm again");
        }
        finally { db.Dispose(); File.Delete(path); }
    }

    // ------------------------------------------------------------------ capacity / ownership

    [Fact]
    public void SeventeenShapes_ShouldEvictFifo_WithoutBreakingAnyResult()
    {
        var (db, path) = CreateDb();
        try
        {
            // 17 distinct supported shapes: 3 x (Count, LongCount, Sum(Id), Sum(Amount), Sum(Other))
            // plus (Count, LongCount) of a fourth entity. The store holds 16, so the first is evicted.
            CallAllShapes(db);

            // The first shape lost its artifact: the next call re-plans, the call after it is warm again.
            ResetPlannerDiagnostics();
            db.From<ScalarRowA>().Count().Should().Be(3);
            PlannerDiagnostics().Should().BeGreaterThan(0, "the oldest entry was evicted by the 17th shape");

            ResetPlannerDiagnostics();
            db.From<ScalarRowA>().Count().Should().Be(3);
            PlannerDiagnostics().Should().Be(0);

            // Every shape is still correct after the eviction churn.
            CallAllShapes(db);
        }
        finally { db.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void TwoContexts_ShouldNotShareArtifacts()
    {
        var (dbA, pathA) = CreateDb();
        var (dbB, pathB) = CreateDb();
        try
        {
            Execute(dbB, "insert into d243_scalar_a (id, amount, other) values (4, 40, 400);");

            // Warm context A on its own data.
            dbA.From<ScalarRowA>().Count().Should().Be(3);
            dbA.From<ScalarRowA>().Sum(x => x.Id).Should().Be(6L);

            // Context B must execute against its own data, never reuse A's invocation state.
            dbB.From<ScalarRowA>().Count().Should().Be(4);
            dbB.From<ScalarRowA>().Sum(x => x.Id).Should().Be(10L);

            // And A still sees its own.
            dbA.From<ScalarRowA>().Count().Should().Be(3);
            dbA.From<ScalarRowA>().Sum(x => x.Id).Should().Be(6L);

            // B is warm on its own shape afterwards: no per-call preparation left.
            ResetPlannerDiagnostics();
            dbB.From<ScalarRowA>().Count().Should().Be(4);
            PlannerDiagnostics().Should().Be(0);
        }
        finally
        {
            dbA.Dispose();
            dbB.Dispose();
            File.Delete(pathA);
            File.Delete(pathB);
        }
    }

    [Fact]
    public void JoinInto_ShouldTakeTheOrdinaryPath_AndCountTheParents()
    {
        var (db, path) = CreateDb();
        try
        {
            EntityBuilder<ParentRow> Parents() =>
                db.From<ParentRow>(b => b.HasMany(p => p.Children, c => c.ParentId));

            // JoinInto makes the builder non-plain, so the scalar terminals keep the ordinary path and
            // the existing semantics: the non-list terminals evaluate the parent only.
            Parents()
                .JoinInto(db.From<ChildRow>(), (p, c) => p.Id == c.ParentId, p => p.Children)
                .Count()
                .Should().Be(3);
            Parents()
                .JoinInto(db.From<ChildRow>(), (p, c) => p.Id == c.ParentId, p => p.Children)
                .Sum(x => x.Id)
                .Should().Be(6);
        }
        finally { db.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void TempTableSource_ShouldTakeTheOrdinaryPath_AndStayCorrect()
    {
        var (db, path) = CreateDb();
        try
        {
            // A temp-table source is materialised lazily, so its scalar terminal uses the local
            // `storeInCache: false` path; the fast path must not reuse anything for it.
            var source = db.From<ScalarRowA>().Select(x => new { x.Id }).AsTempTable();

            db.From(source).Count().Should().Be(3);
            db.From(source).LongCount().Should().Be(3L);
        }
        finally { db.Dispose(); File.Delete(path); }
    }

    // ------------------------------------------------------------------ helpers

    private static void CallAllShapes(SqliteDataContext db)
    {
        db.From<ScalarRowA>().Count().Should().Be(3);
        db.From<ScalarRowA>().LongCount().Should().Be(3L);
        db.From<ScalarRowA>().Sum(x => x.Id).Should().Be(6L);
        db.From<ScalarRowA>().Sum(x => x.Amount).Should().Be(30);
        db.From<ScalarRowA>().Sum(x => x.Other).Should().Be(600L);

        db.From<ScalarRowB>().Count().Should().Be(2);
        db.From<ScalarRowB>().LongCount().Should().Be(2L);
        db.From<ScalarRowB>().Sum(x => x.Id).Should().Be(3L);
        db.From<ScalarRowB>().Sum(x => x.Amount).Should().Be(3);
        db.From<ScalarRowB>().Sum(x => x.Other).Should().Be(3L);

        db.From<ScalarRowC>().Count().Should().Be(2);
        db.From<ScalarRowC>().LongCount().Should().Be(2L);
        db.From<ScalarRowC>().Sum(x => x.Id).Should().Be(3L);
        db.From<ScalarRowC>().Sum(x => x.Amount).Should().Be(3);
        db.From<ScalarRowC>().Sum(x => x.Other).Should().Be(3L);

        db.From<ScalarRowD>().Count().Should().Be(2);
        db.From<ScalarRowD>().LongCount().Should().Be(2L);
    }

    private static (SqliteDataContext Db, string Path) CreateDb()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-d243-scalar-{Guid.NewGuid():N}.db");
        var db = new SqliteDataContext($"Data Source={path}", new DataContextBuilder());
        db.EnsureConnectionOpen();
        Execute(db, Schema);
        return (db, path);
    }

    private static void Execute(SqliteDataContext db, string sql)
    {
        using var command = db.GetConnection().CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    /// <summary>Resets the planner's process-wide pre-preparation diagnostics (the `fast path ran` oracle).</summary>
    private static void ResetPlannerDiagnostics() => QueryPlanner.ResetCounters();

    /// <summary>All pre-preparation diagnostics of a fresh command; zero means no command was prepared.</summary>
    private static int PlannerDiagnostics()
        => QueryPlanner.RecipePublishes
            + QueryPlanner.RecipeReuses
            + QueryPlanner.RecipeRejections
            + QueryPlanner.RecipeApplyMismatches;

    /// <summary>
    /// Counts <see cref="IQueryInterceptor.CommandInitialized"/> for one context: the planning axis raises
    /// it once when it builds a query command and never on a cache hit, so the counter tells "the ordinary
    /// prepared path built a command for this call" from "the warm artifact executed a plan it did not
    /// build". A public event, unlike the artifact store, which is not observable from a test.
    /// </summary>
    private sealed class PreparedCommandCounter : IQueryInterceptor
    {
        private int _count;

        internal int Count => Volatile.Read(ref _count);

        internal void Reset() => Volatile.Write(ref _count, 0);

        public void CommandInitialized(CommandEventData eventData, DbCommand command)
            => Interlocked.Increment(ref _count);
    }

    // ------------------------------------------------------------------ entities

    [SqlTable("d243_scalar_a")]
    public sealed class ScalarRowA
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("amount")]
        public int? Amount { get; set; }

        [Column("other")]
        public long Other { get; set; }
    }

    [SqlTable("d243_scalar_b")]
    public sealed class ScalarRowB
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("amount")]
        public int? Amount { get; set; }

        [Column("other")]
        public long Other { get; set; }
    }

    [SqlTable("d243_scalar_c")]
    public sealed class ScalarRowC
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("amount")]
        public int? Amount { get; set; }

        [Column("other")]
        public long Other { get; set; }
    }

    [SqlTable("d243_scalar_d")]
    public sealed class ScalarRowD
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("amount")]
        public int? Amount { get; set; }

        [Column("other")]
        public long Other { get; set; }
    }

    [SqlTable("d243_scalar_empty")]
    public sealed class EmptyRow
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("amount")]
        public int? Amount { get; set; }
    }

    [SqlTable("d243_scalar_nulls")]
    public sealed class NullRow
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("amount")]
        public int? Amount { get; set; }
    }

    [SqlTable("d243_filter_rows")]
    public sealed class FilterRow
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("amount")]
        public int? Amount { get; set; }
    }

    [SqlTable("d243_tenant_rows")]
    public sealed class TenantRow
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }
    }

    [SqlTable("d243_parent")]
    public sealed class ParentRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        public ICollection<ChildRow> Children { get; set; } = new List<ChildRow>();
    }

    [SqlTable("d243_child")]
    public sealed class ChildRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("parent_id")]
        public int ParentId { get; set; }

        [Column("name")]
        public string? Name { get; set; }
    }
}
