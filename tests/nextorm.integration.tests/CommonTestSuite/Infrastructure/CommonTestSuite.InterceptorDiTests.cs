using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NextORM.ClickHouse;
using NextORM.Core;
using NextORM.MySql;
using NextORM.Postgres;
using NextORM.Sqlite;
using NextORM.SqlServer;

namespace NextORM.Integration.Tests;

/// <summary>
/// Provider-agnostic DI interceptor wiring (#221): a service-key interceptor registered on a
/// DI-created builder is resolved from the builder's scope and observes a real query on each
/// provider. The per-provider branch selects the provider extension, so the test runs unchanged on
/// SQLite, PostgreSQL, SQL Server and MySQL/MariaDB through <see cref="CommonTestSuite"/>; ClickHouse
/// has no shared-suite subclass and gets its own equivalent below.
/// </summary>
public abstract partial class CommonTestSuite
{
    [Fact]
    [Trait("Category", "T221")]
    public void DiRegisteredInterceptor_ObservesProviderQuery()
    {
        Provider.EnsureSeeded();

        var services = new ServiceCollection();
        services.AddSingleton<DiCreationCounters>();
        services.AddScoped<DiRecordingInterceptor>();
        services.AddNextOrmContext(builder =>
            DiProviderConfigurator.Configure(Provider.Name, builder).AddInterceptor<DiRecordingInterceptor>());
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<IDataContext>();
        var interceptor = scope.ServiceProvider.GetRequiredService<DiRecordingInterceptor>();

        context.PurgeQueryCache();
        context.From<ISimpleEntity>().Select(x => x.Id).ToList();

        interceptor.Initialized.Should().BeTrue();
        interceptor.Executing.Should().BeTrue();
        interceptor.Executed.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "T221")]
    public void DiRegisteredInterceptor_DeduplicatesByType()
    {
        Provider.EnsureSeeded();

        var services = new ServiceCollection();
        services.AddSingleton<DiCreationCounters>();
        services.AddScoped<DiRecordingInterceptor>();
        services.AddNextOrmContext(builder => DiProviderConfigurator
            .Configure(Provider.Name, builder)
            .AddInterceptor<DiRecordingInterceptor>()
            .AddInterceptor<DiRecordingInterceptor>());
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IDataContext>();

        scope.ServiceProvider.GetRequiredService<DiCreationCounters>().Created.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "T221")]
    public void StandaloneTypeInterceptor_FailsBeforeTheFactoryRuns()
    {
        var builder = new DataContextBuilder();
        builder.AddInterceptor<DiRecordingInterceptor>();
        builder.Factory = _ => Provider.CreateContext();

        var act = () => builder.CreateDataContext();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*requires a DI-backed DataContextBuilder*");
    }
}

/// <summary>
/// ClickHouse has no <see cref="CommonTestSuite"/> subclass, so this mirrors the DI wiring test for it.
/// </summary>
[Collection("ClickHouse")]
public sealed class ClickHouseInterceptorDiTests : ProviderTestSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    [Fact]
    [Trait("Category", "T221")]
    public void DiRegisteredInterceptor_ObservesProviderQuery()
    {
        Provider.EnsureSeeded();

        var services = new ServiceCollection();
        services.AddSingleton<DiCreationCounters>();
        services.AddScoped<DiRecordingInterceptor>();
        services.AddNextOrmContext(builder => builder
            .UseClickHouse(ClickHouseContainer.ConnectionString)
            .AddInterceptor<DiRecordingInterceptor>());
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<IDataContext>();
        var interceptor = scope.ServiceProvider.GetRequiredService<DiRecordingInterceptor>();

        context.PurgeQueryCache();
        context.From<ISimpleEntity>().Select(x => x.Id).ToList();

        interceptor.Initialized.Should().BeTrue();
        interceptor.Executed.Should().BeTrue();
    }
}

/// <summary>Counts the interceptor instances the container created, for the deduplication assertion.</summary>
public sealed class DiCreationCounters
{
    public int Created { get; set; }
}

/// <summary>A query interceptor observable through its flags, constructed by the container.</summary>
public sealed class DiRecordingInterceptor : IQueryInterceptor
{
    public DiRecordingInterceptor(DiCreationCounters counters) => counters.Created++;

    public bool Initialized { get; private set; }
    public bool Executing { get; private set; }
    public bool Executed { get; private set; }

    public void CommandInitialized(CommandEventData eventData, System.Data.Common.DbCommand command)
        => Initialized = true;

    public void CommandExecuting(CommandEventData eventData, System.Data.Common.DbCommand command)
        => Executing = true;

    public void CommandExecuted(CommandEventData eventData, System.Data.Common.DbCommand command, TimeSpan elapsed)
        => Executed = true;
}

/// <summary>Selects the provider <c>Use…</c> extension for the shared DI integration test.</summary>
internal static class DiProviderConfigurator
{
    public static DataContextBuilder Configure(string providerName, DataContextBuilder builder) => providerName switch
    {
        "sqlite" => builder.UseSqlite(SqliteTestProvider.DatabasePath),
        "postgres" => builder.UsePostgres(PostgresContainer.ConnectionString),
        "sqlserver" => builder.UseSqlServer(SqlServerContainer.ConnectionString),
        "mysql" => builder.UseMySql(MySqlContainer.ConnectionString),
        "clickhouse" => builder.UseClickHouse(ClickHouseContainer.ConnectionString),
        _ => throw new InvalidOperationException($"No DI provider configuration is registered for provider '{providerName}'.")
    };
}
