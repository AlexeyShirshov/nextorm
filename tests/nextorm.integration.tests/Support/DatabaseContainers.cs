using Xunit;

[assembly: AssemblyFixture(typeof(NextORM.Integration.Tests.DatabaseContainers))]

namespace NextORM.Integration.Tests;

/// <summary>
/// Owns the database containers for the whole test run. Container start is lazy (a container is
/// only created when a test actually needs that provider), so there is nothing to do on
/// initialization; at the end of the run the containers are stopped here, and because reuse is
/// enabled they are kept on disk and started again on the next run.
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
        await MySqlContainer.DisposeContainerAsync().ConfigureAwait(false);
        await MariaDbContainer.DisposeContainerAsync().ConfigureAwait(false);
        await ClickHouseContainer.DisposeContainerAsync().ConfigureAwait(false);
        // T359 (#349): the T348 2022 arm runs inside the same process as the 2025 one, so its
        // container has to be released here as well; without this it stayed Up after every full run
        // (observed for 7 h on the measurement host) and held ~1.5 GB of the shared VM between runs.
        await SqlServer2022Container.DisposeContainerAsync().ConfigureAwait(false);
    }
}
