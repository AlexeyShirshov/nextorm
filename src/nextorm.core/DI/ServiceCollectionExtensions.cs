using Microsoft.Extensions.DependencyInjection;

namespace nextorm.core;

/// <summary>
/// Registration helpers for nextorm contexts.
/// <para>
/// Invariants:
/// <list type="bullet">
/// <item>a concrete context type is registered <b>once per scope</b>: resolving the concrete type and
/// <see cref="IDataContext"/> yields the same instance (previously they were two separate instances);</item>
/// <item>the options delegate is mandatory for factory-based registration — a <see cref="DbContextBuilder"/>
/// without options can never produce a context, so a null delegate now fails at registration time instead of
/// at resolution time.</item>
/// </list>
/// </para>
/// </summary>
public static class ServiceCollectionExtensions
{
    public static void AddNextOrmContext(this IServiceCollection services, Action<IServiceProvider, DbContextBuilder> optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        services.RegisterContextFactory(null, optionsBuilder);
    }

    public static void AddKeyedNextOrmContext(this IServiceCollection services, Action<IServiceProvider, DbContextBuilder> optionsBuilder, object? serviceKey)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        services.RegisterContextFactory(serviceKey, optionsBuilder);
    }

    public static void AddNextOrmContext(this IServiceCollection services, Action<DbContextBuilder> optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        services.RegisterContextFactory(null, (_, builder) => optionsBuilder(builder));
    }

    public static void AddKeyedNextOrmContext(this IServiceCollection services, Action<DbContextBuilder> optionsBuilder, object? serviceKey)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        services.RegisterContextFactory(serviceKey, (_, builder) => optionsBuilder(builder));
    }

    public static void AddNextOrmContext<T>(this IServiceCollection services)
        where T : class, IDataContext
    {
        ArgumentNullException.ThrowIfNull(services);

        services.RegisterContextType<T>(null);
    }

    public static void AddKeyedNextOrmContext<T>(this IServiceCollection services, object? serviceKey)
        where T : class, IDataContext
    {
        ArgumentNullException.ThrowIfNull(services);

        services.RegisterContextType<T>(serviceKey);
    }

    /// <summary>
    /// Options-driven path: registers a scoped <see cref="DbContextBuilder"/> built by
    /// <paramref name="optionsBuilder"/> plus an <see cref="IDataContext"/> factory that turns it into a
    /// context.
    /// </summary>
    private static void RegisterContextFactory(this IServiceCollection services, object? serviceKey, Action<IServiceProvider, DbContextBuilder> optionsBuilder)
    {
        if (serviceKey is null)
        {
            services.AddScoped(sp =>
            {
                var builder = new DbContextBuilder();
                optionsBuilder(sp, builder);
                return builder;
            });

            services.AddScoped(sp => sp.GetRequiredService<DbContextBuilder>().CreateDbContext());
        }
        else
        {
            services.AddKeyedScoped(serviceKey, (sp, _) =>
            {
                var builder = new DbContextBuilder();
                optionsBuilder(sp, builder);
                return builder;
            });

            services.AddKeyedScoped(serviceKey, (sp, k) => sp.GetRequiredKeyedService<DbContextBuilder>(k).CreateDbContext());
        }
    }

    /// <summary>
    /// Type-driven path: registers the concrete context once per scope and forwards
    /// <see cref="IDataContext"/> to it, so both resolve to the same instance.
    /// </summary>
    private static void RegisterContextType<T>(this IServiceCollection services, object? serviceKey)
        where T : class, IDataContext
    {
        if (serviceKey is null)
        {
            services.AddScoped<T>();
            services.AddScoped<IDataContext>(sp => sp.GetRequiredService<T>());
        }
        else
        {
            services.AddKeyedScoped<T>(serviceKey);
            services.AddKeyedScoped<IDataContext>(serviceKey, (sp, k) => sp.GetRequiredKeyedService<T>(k));
        }
    }
}
