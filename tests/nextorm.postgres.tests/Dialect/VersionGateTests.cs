using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>
/// Version-gated behavior of the PostgreSQL context: the ANSI aggregate <c>FILTER</c> clause is emitted
/// from server 9.4 (unset means "today's behavior"), the server version is immutable per concrete
/// context type, and the plan cache does not leak a warmed filtered plan into a version that must
/// refuse it.
/// </summary>
public class VersionGateTests
{
    private static IDataContext Context<TMarker>(Version? serverVersion)
        => PostgresTestContext.CreateVersioned<TMarker>(serverVersion);

    private static string FilteredSql(IDataContext ctx, bool storeInCache)
        => Prepare(
            ctx,
            ctx.From<IComplexEntity>().Select(x => new { C = SqlFunctions.Sql.count(() => x.Id > 1L) }),
            storeInCache);

    private static string Prepare<T>(IDataContext ctx, QueryCommand<T> cmd, bool storeInCache)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, storeInCache, CancellationToken.None))
            .DbCommand.CommandText;

    private static void AssertRefused(IDataContext ctx)
    {
        using (ctx)
            FluentActions.Invoking(() => FilteredSql(ctx, storeInCache: false)).Should().Throw<NotSupportedException>();
    }

    private static void AssertFiltered(IDataContext ctx)
    {
        using (ctx)
            FilteredSql(ctx, storeInCache: false).Should().Contain("filter (where (id > 1))");
    }

    [Fact]
    public void FilteredAggregate_Pg92_ShouldThrow()
        => AssertRefused(Context<Pg92Marker>(new Version(9, 2)));

    [Fact]
    public void FilteredAggregate_Pg93_ShouldThrow()
        => AssertRefused(Context<Pg93Marker>(new Version(9, 3)));

    [Fact]
    public void FilteredAggregate_Pg93Patch_ShouldThrow()
        => AssertRefused(Context<Pg93PatchMarker>(new Version(9, 3, 5)));

    [Fact]
    public void FilteredAggregate_Pg94_ShouldEmitFilter()
        => AssertFiltered(Context<Pg94Marker>(new Version(9, 4)));

    [Fact]
    public void FilteredAggregate_Pg94Patch_ShouldEmitFilter()
        => AssertFiltered(Context<Pg94PatchMarker>(new Version(9, 4, 2)));

    [Fact]
    public void FilteredAggregate_Pg16_ShouldEmitFilter()
        => AssertFiltered(Context<Pg16Marker>(new Version(16, 0)));

    [Fact]
    public void FilteredAggregate_Unset_ShouldEmitFilter()
        => AssertFiltered(Context<UnsetMarker>(null));

    [Fact]
    public void SameType_EqualVersionDistinctObjects_ShouldConstruct()
    {
        // Two distinct Version objects with the same value must be accepted.
        using (Context<EqualVersionMarker>(new Version(9, 4)))
        {
        }

        using (Context<EqualVersionMarker>(new Version(9, 4)))
        {
        }
    }

    [Fact]
    public void SameType_UnequalVersions_ShouldThrowNamingSubclass()
    {
        using (Context<UnequalVersionMarker>(new Version(9, 4)))
        {
        }

        Action act = () => Context<UnequalVersionMarker>(new Version(9, 6)).Dispose();

        act.Should().Throw<InvalidOperationException>().WithMessage("*distinct DataContext subclass*");
    }

    [Fact]
    public void SameType_UnsetThenExplicit_ShouldThrowNamingSubclass()
    {
        using (Context<UnsetFirstMarker>(null))
        {
        }

        Action act = () => Context<UnsetFirstMarker>(new Version(9, 4)).Dispose();

        act.Should().Throw<InvalidOperationException>().WithMessage("*distinct DataContext subclass*");
    }

    [Fact]
    public void SameType_ExplicitThenUnset_ShouldThrowNamingSubclass()
    {
        using (Context<ExplicitFirstMarker>(new Version(9, 4)))
        {
        }

        Action act = () => Context<ExplicitFirstMarker>(null).Dispose();

        act.Should().Throw<InvalidOperationException>().WithMessage("*distinct DataContext subclass*");
    }

    [Fact]
    public async Task ConcurrentConflictingConstruction_ShouldPickOneWinnerWithoutOverwrite()
    {
        var versions = new[] { new Version(9, 2), new Version(9, 3), new Version(9, 4), new Version(9, 6) };
        var results = new (Version Version, InvalidOperationException? Error)[versions.Length];

        using var start = new ManualResetEventSlim(false);
        var tasks = new Task[versions.Length];

        for (var i = 0; i < versions.Length; i++)
        {
            var index = i;
            tasks[i] = Task.Run(() =>
            {
                start.Wait();
                try
                {
                    using var ctx = Context<ConcurrentMarker>(versions[index]);
                    results[index] = (versions[index], null);
                }
                catch (InvalidOperationException ex)
                {
                    results[index] = (versions[index], ex);
                }
            }, TestContext.Current.CancellationToken);
        }

        start.Set();
        await Task.WhenAll(tasks);

        var winner = results.Single(r => r.Error is null).Version;
        results.Count(r => r.Error is not null).Should().Be(versions.Length - 1);

        // The winner is stable: replaying it succeeds...
        using (var replay = Context<ConcurrentMarker>(winner))
            ((PostgresDataContext)replay).Dialect.Should().NotBeNull();

        // ...and a losing version cannot overwrite the registered one.
        var loser = versions.First(v => v != winner);
        Action act = () => Context<ConcurrentMarker>(loser).Dispose();

        act.Should().Throw<InvalidOperationException>().WithMessage("*distinct DataContext subclass*");
    }

    [Fact]
    public void CacheIsolation_SupportedThenUnsupported_ShouldNotLeak()
    {
        // Warm a supported-version filtered query through the real preparation path (stored in the plan
        // cache under this context type)...
        using (var supported = Context<CacheSupA>(new Version(9, 4)))
        {
            FilteredSql(supported, storeInCache: true).Should().Contain("filter (where (id > 1))");

            // ...and prove the plan-cache path is really exercised: a warm preparation of the same
            // command is a cache hit and returns the exact same prepared instance, not a rebuild.
            var warmCommand = supported.From<IComplexEntity>()
                .Select(x => new { C = SqlFunctions.Sql.count(() => x.Id > 1L) });
            var warmed = supported.GetPreparedQueryCommand(warmCommand, false, storeInCache: true, CancellationToken.None);
            var reused = supported.GetPreparedQueryCommand(warmCommand, false, storeInCache: true, CancellationToken.None);
            reused.Should().BeSameAs(warmed);
        }

        // ...an unsupported-version context must still refuse the gate, not reuse the warmed plan.
        using (var unsupported = Context<CacheUnA>(new Version(9, 3)))
            FluentActions.Invoking(() => FilteredSql(unsupported, storeInCache: true))
                .Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void CacheIsolation_UnsupportedFirstThenSupported_ShouldStillRefuse()
    {
        using (var unsupported = Context<CacheUnB>(new Version(9, 3)))
            FluentActions.Invoking(() => FilteredSql(unsupported, storeInCache: true))
                .Should().Throw<NotSupportedException>();

        using (var supported = Context<CacheSupB>(new Version(9, 4)))
        {
            FilteredSql(supported, storeInCache: true).Should().Contain("filter (where (id > 1))");

            // The real cache path is exercised on the supported type too: a warm command reuses the plan.
            var warmCommand = supported.From<IComplexEntity>()
                .Select(x => new { C = SqlFunctions.Sql.count(() => x.Id > 1L) });
            var warmed = supported.GetPreparedQueryCommand(warmCommand, false, storeInCache: true, CancellationToken.None);
            var reused = supported.GetPreparedQueryCommand(warmCommand, false, storeInCache: true, CancellationToken.None);
            reused.Should().BeSameAs(warmed);
        }

        using (var unsupportedAgain = Context<CacheUnB>(new Version(9, 3)))
            FluentActions.Invoking(() => FilteredSql(unsupportedAgain, storeInCache: true))
                .Should().Throw<NotSupportedException>();
    }

    private sealed class Pg92Marker
    {
    }

    private sealed class Pg93Marker
    {
    }

    private sealed class Pg93PatchMarker
    {
    }

    private sealed class Pg94Marker
    {
    }

    private sealed class Pg94PatchMarker
    {
    }

    private sealed class Pg16Marker
    {
    }

    private sealed class UnsetMarker
    {
    }

    private sealed class EqualVersionMarker
    {
    }

    private sealed class UnequalVersionMarker
    {
    }

    private sealed class UnsetFirstMarker
    {
    }

    private sealed class ExplicitFirstMarker
    {
    }

    private sealed class ConcurrentMarker
    {
    }

    private sealed class CacheSupA
    {
    }

    private sealed class CacheUnA
    {
    }

    private sealed class CacheSupB
    {
    }

    private sealed class CacheUnB
    {
    }
}
