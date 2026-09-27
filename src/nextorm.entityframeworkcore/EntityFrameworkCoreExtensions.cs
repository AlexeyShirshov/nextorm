using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using NextORM.Core;
using NextORM.MySql;
using NextORM.Postgres;
using NextORM.Sqlite;
using NextORM.SqlServer;

namespace NextORM.EntityFrameworkCore;

/// <summary>
/// Bridges an EF Core <see cref="DbContext"/> to a nextorm <see cref="IDataContext"/>: the nextorm
/// context runs on the same connection, enlists EF's active transaction, and reads its mapping from
/// the EF model.
/// </summary>
/// <remarks>
/// The bridge is read-only: nextorm opens a closed borrowed connection on demand and never closes,
/// commits or rolls back the connection or the transaction it borrowed, so EF keeps ownership of both
/// for their whole lifetime.
/// </remarks>
public static class EntityFrameworkCoreExtensions
{
    private const string SupportedProvidersMessage =
        "Supported EF Core providers are: Npgsql.EntityFrameworkCore.PostgreSQL (PostgreSQL), " +
        "Microsoft.EntityFrameworkCore.SqlServer, Pomelo.EntityFrameworkCore.MySql (MySQL and MariaDB), " +
        "and Microsoft.EntityFrameworkCore.Sqlite.";

    /// <summary>
    /// Creates a nextorm <see cref="IDataContext"/> over the EF Core connection of
    /// <paramref name="dbContext"/>, using the mapping read from <paramref name="dbContext"/>'s model.
    /// </summary>
    /// <param name="dbContext">The EF Core context whose connection, model and current transaction are reused.</param>
    /// <param name="configure">
    /// Optional nextorm configuration applied after the provider is selected from
    /// <c>dbContext.Database.ProviderName</c>; use it to override provider defaults.
    /// </param>
    /// <returns>
    /// An <see cref="IDataContext"/> that executes on the EF connection and, when
    /// <c>dbContext.Database.CurrentTransaction</c> is active, inside that transaction.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="dbContext"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The EF provider reported by <c>ProviderName</c> is not one of the supported providers.</exception>
    public static IDataContext CreateNextOrmContext(this DbContext dbContext, Action<DataContextBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        return CreateContext(dbContext, configure);
    }

    /// <summary>
    /// Shared pipeline for every public entry point: resolves the provider, borrows EF's connection and
    /// current transaction, registers the EF model mapping and builds a read-only nextorm context.
    /// </summary>
    internal static IDataContext CreateContext(DbContext dbContext, Action<DataContextBuilder>? configure)
    {
        // Resolve and validate the provider before touching the connection. The non-relational InMemory
        // provider has no DbConnection, so it must fail with the documented unsupported-provider error
        // rather than EF's relational one.
        var providerName = dbContext.Database.ProviderName;
        var provider = ResolveProvider(providerName);

        if (!dbContext.Database.IsRelational())
            throw new InvalidOperationException(
                $"The EF Core provider '{providerName}' is not relational; nextorm requires a relational EF Core provider. {SupportedProvidersMessage}");

        var connection = dbContext.Database.GetDbConnection();
        var builder = new DataContextBuilder();

        ConfigureProvider(builder, provider, connection);
        configure?.Invoke(builder);

        NextOrmModelMapper.Register(dbContext.Model);

        var context = builder.CreateDataContext();

        var efTransaction = dbContext.Database.CurrentTransaction;
        if (efTransaction is not null)
            ((ITransactionManager)context).UseTransaction(efTransaction.GetDbTransaction());

        return context;
    }

    /// <summary>
    /// Reads the configure delegate stored by <c>UseNextOrm</c> from the context's options, or
    /// <see langword="null"/> when the bridge was not configured through
    /// <see cref="DbContextOptionsBuilder"/>.
    /// </summary>
    internal static Action<DataContextBuilder>? FindConfiguredDelegate(DbContext dbContext)
        => dbContext.GetService<IDbContextOptions>().FindExtension<NextOrmOptionsExtension>()?.Configure;

    /// <summary>
    /// Maps <paramref name="providerName"/> to a nextorm provider by exact EF provider name.
    /// </summary>
    /// <remarks>
    /// Matching is exact (ordinal) on purpose: a substring match used to accept look-alike names. Only
    /// the four shipped EF providers are recognised.
    /// </remarks>
    private static SupportedProvider ResolveProvider(string? providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            throw new InvalidOperationException($"The EF Core provider name is not set. {SupportedProvidersMessage}");

        return providerName switch
        {
            "Npgsql.EntityFrameworkCore.PostgreSQL" => SupportedProvider.Postgres,
            "Microsoft.EntityFrameworkCore.SqlServer" => SupportedProvider.SqlServer,
            "Pomelo.EntityFrameworkCore.MySql" => SupportedProvider.MySql,
            "Microsoft.EntityFrameworkCore.Sqlite" => SupportedProvider.Sqlite,
            _ => throw new InvalidOperationException(
                $"The EF Core provider '{providerName}' is not supported. {SupportedProvidersMessage}"),
        };
    }

    private static void ConfigureProvider(DataContextBuilder builder, SupportedProvider provider, DbConnection connection)
    {
        switch (provider)
        {
            case SupportedProvider.Postgres:
                builder.UsePostgres(connection);
                break;
            case SupportedProvider.SqlServer:
                builder.UseSqlServer(connection);
                break;
            case SupportedProvider.MySql:
                builder.UseMySql(connection);
                break;
            case SupportedProvider.Sqlite:
                builder.UseSqlite(connection);
                break;
            default:
                throw new InvalidOperationException(
                    $"No nextorm provider is wired for the EF Core provider '{provider}'. {SupportedProvidersMessage}");
        }
    }

    private enum SupportedProvider
    {
        Postgres,
        SqlServer,
        MySql,
        Sqlite,
    }
}
