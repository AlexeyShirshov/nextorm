using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using NextORM.Core;

namespace NextORM.EntityFrameworkCore;

/// <summary>
/// Registers the EF Core-to-nextorm bridge on <see cref="DbContextOptionsBuilder"/> and resolves a
/// nextorm <see cref="IDataContext"/> from a <see cref="DbContext"/> or the DI container.
/// </summary>
/// <remarks>
/// The bridge never owns the EF connection or transaction: it borrows both and leaves EF in control, so
/// disposing an <see cref="IDataContext"/> resolved through these methods does not close or dispose the
/// <see cref="DbContext"/>.
/// </remarks>
public static class NextOrmDbContextExtensions
{
    /// <summary>
    /// Stores the optional nextorm configuration on <paramref name="optionsBuilder"/>, so that
    /// <see cref="GetNextOrmContext"/> can build a context without taking the delegate again.
    /// </summary>
    /// <param name="optionsBuilder">The EF Core options builder to extend.</param>
    /// <param name="configure">Optional nextorm configuration applied after the provider is selected.</param>
    /// <returns>The same <paramref name="optionsBuilder"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="optionsBuilder"/> is <see langword="null"/>.</exception>
    public static DbContextOptionsBuilder UseNextOrm(this DbContextOptionsBuilder optionsBuilder, Action<DataContextBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(new NextOrmOptionsExtension(configure));

        return optionsBuilder;
    }

    /// <summary>
    /// Stores the optional nextorm configuration on the strongly typed <paramref name="optionsBuilder"/>,
    /// so that <see cref="GetNextOrmContext"/> can build a context without taking the delegate again.
    /// </summary>
    /// <typeparam name="TContext">The EF Core context type the options belong to.</typeparam>
    /// <param name="optionsBuilder">The EF Core options builder to extend.</param>
    /// <param name="configure">Optional nextorm configuration applied after the provider is selected.</param>
    /// <returns>The same <paramref name="optionsBuilder"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="optionsBuilder"/> is <see langword="null"/>.</exception>
    public static DbContextOptionsBuilder<TContext> UseNextOrm<TContext>(this DbContextOptionsBuilder<TContext> optionsBuilder, Action<DataContextBuilder>? configure = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        return (DbContextOptionsBuilder<TContext>)UseNextOrm((DbContextOptionsBuilder)optionsBuilder, configure);
    }

    /// <summary>
    /// Creates a nextorm <see cref="IDataContext"/> over <paramref name="dbContext"/>'s
    /// connection, current transaction and model, applying the configuration stored by
    /// <see cref="UseNextOrm"/> when the context's options carry it.
    /// </summary>
    /// <param name="dbContext">The EF Core context whose connection, model and current transaction are reused.</param>
    /// <returns>
    /// An <see cref="IDataContext"/> that executes on the EF connection and inside EF's current
    /// transaction when one is active.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="dbContext"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The EF provider reported by <c>ProviderName</c> is not one of the supported providers.</exception>
    public static IDataContext GetNextOrmContext(this DbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        return EntityFrameworkCoreExtensions.CreateContext(
            dbContext,
            EntityFrameworkCoreExtensions.FindConfiguredDelegate(dbContext));
    }

    /// <summary>
    /// Registers a scoped <see cref="IDataContext"/> that is built from the scope's
    /// <typeparamref name="TDbContext"/> via <see cref="GetNextOrmContext"/>.
    /// </summary>
    /// <typeparam name="TDbContext">The EF Core context type already registered in the container.</typeparam>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configure">
    /// Optional nextorm configuration applied after any delegate stored by <see cref="UseNextOrm"/>.
    /// </param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddNextOrmFromDbContext<TDbContext>(this IServiceCollection services, Action<DataContextBuilder>? configure = null)
        where TDbContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IDataContext>(serviceProvider =>
        {
            var dbContext = serviceProvider.GetRequiredService<TDbContext>();
            var stored = EntityFrameworkCoreExtensions.FindConfiguredDelegate(dbContext);

            if (configure is null)
                return EntityFrameworkCoreExtensions.CreateContext(dbContext, stored);

            if (stored is null)
                return EntityFrameworkCoreExtensions.CreateContext(dbContext, configure);

            return EntityFrameworkCoreExtensions.CreateContext(dbContext, builder =>
            {
                stored(builder);
                configure(builder);
            });
        });

        return services;
    }
}
