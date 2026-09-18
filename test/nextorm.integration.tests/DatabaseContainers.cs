using Xunit;

[assembly: AssemblyFixture(typeof(nextorm.integration.tests.DatabaseContainers))]

namespace nextorm.integration.tests;

/// <summary>
/// Owns the disposable database containers for the whole test run. Container start is lazy (a
/// container is only created when a test actually needs that provider), so there is nothing to do
/// on initialization; the containers are released here at the end of the run.
/// </summary>
/// <remarks>
/// Disposal deliberately does not happen in a <see cref="AppDomain.ProcessExit"/> handler: the
/// Testcontainers modules leave background threads behind, and blocking on them while the process
/// is exiting makes the test host report leftover foreground threads, which fails the run with a
/// non-zero exit code even though every test passed.
/// </remarks>
internal sealed class DatabaseContainers : IAsyncLifetime
{
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        await PostgresContainer.DisposeContainerAsync().ConfigureAwait(false);
        await SqlServerContainer.DisposeContainerAsync().ConfigureAwait(false);
        await ClickHouseContainer.DisposeContainerAsync().ConfigureAwait(false);
    }
}
