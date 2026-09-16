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
                              "insert into simple_entity (id) values (42);";
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
}
