using FluentAssertions;
using Microsoft.Data.Sqlite;
using nextorm.core;
using nextorm.sqlite;

namespace nextorm.sqlite.tests;

/// <summary>
/// Characterisation tests for the two reuse paths (implicit plan cache vs explicit Prepare) against a
/// real database. A temp file db is used because ':memory:' is per-connection, so the setup connection
/// and the context would not see the same database.
/// </summary>
public class PlanCacheTests
{
    private static (IDataContext Ctx, string Path) CreateDb()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-plancache-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "create table simple_entity (id integer primary key);" +
                              "insert into simple_entity (id) values (42);" +
                              "create table complex_entity (id integer primary key);" +
                              "insert into complex_entity (id) values (42);";
            cmd.ExecuteNonQuery();
        }

        var ctx = new SqliteDbContext($"Data Source={path}", new DbContextBuilder());
        ctx.EnsureConnectionOpen();
        return (ctx, path);
    }

    private static async Task<List<int>> DrainAsync(IAsyncEnumerable<int> source, CancellationToken cancellationToken)
    {
        var result = new List<int>();
        await foreach (var item in source.WithCancellation(cancellationToken))
            result.Add(item);
        return result;
    }

    /// <summary>
    /// Regression test: the cache-hit branch of GetPreparedQueryCommand used to ignore
    /// <c>createEnumerator</c>, so a shape first executed with a buffered terminal (createEnumerator: false)
    /// was cached without a ResultSetEnumerator and a later streaming call on the same shape threw
    /// NullReferenceException (DbContext.CreateAsyncEnumerator dereferences <c>compiledQuery.Enumerator!</c>).
    /// </summary>
    [Fact]
    public async Task BufferedThenAsyncStreaming_OnTheSameShape_ShouldNotThrow()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            // Buffered terminal: GetPreparedQueryCommand(createEnumerator: false) caches the plan
            // without a ResultSetEnumerator.
            ctx.Create<ISimpleEntity>().Select(x => x.Id).ToList().Should().Equal(42);

            // Streaming terminal on the same shape: the cache hit must create the enumerator on demand.
            var act = async () => await DrainAsync(
                ctx.Create<ISimpleEntity>().Select(x => x.Id).ToAsyncEnumerable(TestContext.Current.CancellationToken),
                TestContext.Current.CancellationToken);

            await act.Should().NotThrowAsync();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    /// <summary>
    /// Same root cause as <see cref="BufferedThenAsyncStreaming_OnTheSameShape_ShouldNotThrow"/> on the
    /// synchronous path: DbContext.CreateEnumerator also dereferences <c>compiledQuery.Enumerator!</c>.
    /// </summary>
    [Fact]
    public void BufferedThenSyncStreaming_OnTheSameShape_ShouldNotThrow()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            ctx.Create<ISimpleEntity>().Select(x => x.Id).ToList().Should().Equal(42);

            // ToEnumerable -> GetPreparedQueryCommand(createEnumerator: true) -> CreateEnumerator.
            ctx.Create<ISimpleEntity>().Select(x => x.Id).ToEnumerable().Should().Equal(42);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Prepared_NonStreaming_ShouldSupportStreaming()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            // Prepare(nonStreamUsing: false) is the only way to get a streamable prepared command.
            var prepared = ctx.Create<ISimpleEntity>().Select(x => x.Id)
                .Prepare(nonStreamUsing: false, TestContext.Current.CancellationToken);

            var rows = await DrainAsync(
                prepared.ToAsyncEnumerable(ctx, TestContext.Current.CancellationToken),
                TestContext.Current.CancellationToken);

            rows.Should().Equal(42);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Prepared_Default_ShouldBeBufferedAndReusable()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            // Prepare() defaults to nonStreamUsing: true (buffered/scalar only).
            var prepared = ctx.Create<ISimpleEntity>().Select(x => x.Id)
                .Prepare(cancellationToken: TestContext.Current.CancellationToken);

            prepared.ToList(ctx).Should().Equal(42);
            prepared.ToList(ctx).Should().Equal(42);
            prepared.First(ctx).Should().Be(42);
            prepared.ExecuteScalar(ctx, throwIfNull: true).Should().Be(42);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    /// <summary>
    /// The default Prepare() is optimised for buffered/scalar results and leaves no enumerator, so
    /// streaming must fail with an actionable message instead of a NullReferenceException.
    /// </summary>
    [Fact]
    public async Task Prepared_Default_ShouldNotBeStreamable()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            var prepared = ctx.Create<ISimpleEntity>().Select(x => x.Id)
                .Prepare(cancellationToken: TestContext.Current.CancellationToken);

            // Buffered terminals are fine with the very same command.
            prepared.ToList(ctx).Should().Equal(42);

            var asyncAct = async () => await DrainAsync(
                prepared.ToAsyncEnumerable(ctx, TestContext.Current.CancellationToken),
                TestContext.Current.CancellationToken);

            (await asyncAct.Should().ThrowAsync<InvalidOperationException>())
                .WithMessage("*nonStreamUsing: false*");

            // ToEnumerable is a lazy iterator, so the failure surfaces on enumeration, not on the call.
            var syncAct = () => prepared.ToEnumerable(ctx).ToList();

            syncAct.Should().Throw<InvalidOperationException>()
                .WithMessage("*nonStreamUsing: false*");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    /// <summary>
    /// A cached plan with a CTE must re-extract the CTE's captured parameter on a cache hit: the
    /// parameter walks through the <c>with</c> clause, not only the outer statement.
    /// </summary>
    [Fact]
    public void Cte_WithCapturedParam_RepeatedExecution_ShouldRefreshParam()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            // The same plan shape is executed twice with a different captured value: the first matches
            // the single seeded row (42 > 0), the second excludes it (42 > 100 is false).
            foreach (var threshold in new[] { 0, 100 })
            {
                var captured = threshold;

                var rows = ctx
                    .With("c", ctx.Create<ISimpleEntity>().Where(x => x.Id > captured).Select(x => new { x.Id }))
                    .From("c")
                    .Select(x => new { id = x["id"].AsInt })
                    .ToList();

                if (threshold == 0)
                    rows.Select(r => r.id).Should().Equal(42);
                else
                    rows.Should().BeEmpty();
            }
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    /// <summary>
    /// The CTE definitions take part in the plan cache key, so two queries that differ only in the CTE
    /// body are not allowed to share a cached plan.
    /// </summary>
    [Fact]
    public void Cte_DifferentDefinitions_ShouldNotSharePlan()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            var matching = ctx
                .With("c", ctx.Create<ISimpleEntity>().Where(x => x.Id > 0).Select(x => new { x.Id }))
                .From("c")
                .Select(x => new { id = x["id"].AsInt })
                .ToList();

            var empty = ctx
                .With("c", ctx.Create<ISimpleEntity>().Where(x => x.Id > 1000).Select(x => new { x.Id }))
                .From("c")
                .Select(x => new { id = x["id"].AsInt })
                .ToList();

            matching.Select(r => r.id).Should().Equal(42);
            empty.Should().BeEmpty();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Prepared_ShouldNotPopulateThePlanCache()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            // Prepare() passes storeInCache: false, so it must not add an entry to the plan cache.
            var prepared = ctx.Create<ISimpleEntity>().Select(x => x.Id)
                .Prepare(cancellationToken: TestContext.Current.CancellationToken);
            prepared.ToList(ctx).Should().Equal(42);

            var first = prepared.ToList(ctx);
            var second = ctx.Create<ISimpleEntity>().Select(x => x.Id).ToList();

            first.Should().Equal(second);
            second.Should().Equal(42);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    /// <summary>
    /// Regression test for set operations (INTERSECT/EXCEPT): the right-hand query is a fresh
    /// <see cref="QueryCommand"/> on every call, so the plan key must hash that command structurally.
    /// It used to call the parameterless <c>GetHashCode()</c>, which resolved to
    /// <c>object.GetHashCode()</c> on the comparer instance and therefore changed for every freshly
    /// built chain, making the warm path miss the plan cache and rebuild the SQL each time.
    /// </summary>
    [Fact]
    public void SetOperation_FreshCommand_ShouldReuseCachedPlan()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            static IPreparedQueryCommand<int> PrepareIntersect(IDataContext ctx)
            {
                var cmd = ctx.Create<ISimpleEntity>().Select(s => s.Id)
                    .Intersect(ctx.Create<IComplexEntity>().Select(c => (int)c.Id));
                return ctx.GetPreparedQueryCommand(cmd, false, true, TestContext.Current.CancellationToken);
            }

            static IPreparedQueryCommand<int> PrepareExcept(IDataContext ctx)
            {
                var cmd = ctx.Create<ISimpleEntity>().Select(s => s.Id)
                    .Except(ctx.Create<IComplexEntity>().Select(c => (int)c.Id));
                return ctx.GetPreparedQueryCommand(cmd, false, true, TestContext.Current.CancellationToken);
            }

            var intersect1 = PrepareIntersect(ctx);
            var intersect2 = PrepareIntersect(ctx);
            ReferenceEquals(intersect1, intersect2).Should()
                .BeTrue("an equal INTERSECT must reuse the cached plan");
            intersect1.ToList(ctx).Should().Equal(42);
            intersect2.ToList(ctx).Should().Equal(42);

            var except1 = PrepareExcept(ctx);
            var except2 = PrepareExcept(ctx);
            ReferenceEquals(except1, except2).Should()
                .BeTrue("an equal EXCEPT must reuse the cached plan");
            except1.ToList(ctx).Should().BeEmpty();
            except2.ToList(ctx).Should().BeEmpty();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    public sealed class CteNumberRow
    {
        public int n { get; set; }
    }

    /// <summary>
    /// Regression test for recursive CTEs: the CTE body is a union of two fresh commands, so the CTE
    /// plan hash (which folds the body's plan) has to be stable across freshly built but equivalent
    /// chains. The recursive body used to inherit the identity-based union hash and never matched.
    /// </summary>
    [Fact]
    public void RecursiveCte_FreshCommand_ShouldReuseCachedPlan()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            static IPreparedQueryCommand<int> Prepare(IDataContext ctx)
            {
                var anchor = ctx.Create<ISimpleEntity>().Where(s => s.Id == 42).Select(s => new CteNumberRow { n = s.Id });
                var step = ctx.From("nums").Where(t => t["n"].AsInt < 45).Select(t => new CteNumberRow { n = t["n"].AsInt + 1 });
                var cmd = ctx.WithRecursive("nums", anchor.UnionAll(step)).From("nums").Select(t => t["n"].AsInt);
                return ctx.GetPreparedQueryCommand(cmd, false, true, TestContext.Current.CancellationToken);
            }

            var first = Prepare(ctx);
            var second = Prepare(ctx);

            ReferenceEquals(first, second).Should()
                .BeTrue("an equal recursive CTE must reuse the cached plan");
            first.ToList(ctx).Should().Equal(42, 43, 44, 45);
            second.ToList(ctx).Should().Equal(42, 43, 44, 45);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }
}
