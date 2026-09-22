using Microsoft.Extensions.DependencyInjection;

namespace NextORM.Core;

/// <summary>
/// Registration helpers for nextorm contexts.
/// <para>
/// Invariants:
/// <list type="bullet">
/// <item>a concrete context type is registered <b>once per scope</b>: resolving the concrete type and
/// <see cref="IDataContext"/> yields the same instance (previously they were two separate instances);</item>
/// <item>the options delegate is mandatory for factory-based registration — a <see cref="DataContextBuilder"/>
/// without options can never produce a context, so a null delegate now fails at registration time instead of
/// at resolution time.</item>
/// </list>
/// </para>
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers an unkeyed <see cref="IDataContext"/> (and its scoped <see cref="DataContextBuilder"/>)
    /// configured by <paramref name="optionsBuilder"/>, which may resolve services from the provider.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="optionsBuilder">Delegate that configures the builder using the current <see cref="IServiceProvider"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="optionsBuilder"/> is <see langword="null"/>.</exception>
    public static void AddNextOrmContext(this IServiceCollection services, Action<IServiceProvider, DataContextBuilder> optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        services.RegisterContextFactory(null, optionsBuilder);
    }

    /// <summary>
    /// Registers a keyed <see cref="IDataContext"/> (and its scoped <see cref="DataContextBuilder"/>)
    /// configured by <paramref name="optionsBuilder"/>, which may resolve services from the provider.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="optionsBuilder">Delegate that configures the builder using the current <see cref="IServiceProvider"/>.</param>
    /// <param name="serviceKey">The key under which the context is registered.</param>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="optionsBuilder"/> is <see langword="null"/>.</exception>
    public static void AddKeyedNextOrmContext(this IServiceCollection services, Action<IServiceProvider, DataContextBuilder> optionsBuilder, object? serviceKey)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        services.RegisterContextFactory(serviceKey, optionsBuilder);
    }

    /// <summary>
    /// Registers an unkeyed <see cref="IDataContext"/> (and its scoped <see cref="DataContextBuilder"/>)
    /// configured by <paramref name="optionsBuilder"/>.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="optionsBuilder">Delegate that configures the builder.</param>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="optionsBuilder"/> is <see langword="null"/>.</exception>
    public static void AddNextOrmContext(this IServiceCollection services, Action<DataContextBuilder> optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        services.RegisterContextFactory(null, (_, builder) => optionsBuilder(builder));
    }

    /// <summary>
    /// Registers a keyed <see cref="IDataContext"/> (and its scoped <see cref="DataContextBuilder"/>)
    /// configured by <paramref name="optionsBuilder"/>.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="optionsBuilder">Delegate that configures the builder.</param>
    /// <param name="serviceKey">The key under which the context is registered.</param>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="optionsBuilder"/> is <see langword="null"/>.</exception>
    public static void AddKeyedNextOrmContext(this IServiceCollection services, Action<DataContextBuilder> optionsBuilder, object? serviceKey)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        services.RegisterContextFactory(serviceKey, (_, builder) => optionsBuilder(builder));
    }

    /// <summary>
    /// Registers the concrete context type <typeparamref name="T"/> once per scope and forwards
    /// <see cref="IDataContext"/> to it, so both resolve to the same instance.
    /// </summary>
    /// <typeparam name="T">The concrete context type to register.</typeparam>
    /// <param name="services">The service collection to register into.</param>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static void AddNextOrmContext<T>(this IServiceCollection services)
        where T : class, IDataContext
    {
        ArgumentNullException.ThrowIfNull(services);

        services.RegisterContextType<T>(null);
    }

    /// <summary>
    /// Registers the concrete context type <typeparamref name="T"/> under a key once per scope and
    /// forwards the keyed <see cref="IDataContext"/> to it, so both resolve to the same instance.
    /// </summary>
    /// <typeparam name="T">The concrete context type to register.</typeparam>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="serviceKey">The key under which the context is registered.</param>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static void AddKeyedNextOrmContext<T>(this IServiceCollection services, object? serviceKey)
        where T : class, IDataContext
    {
        ArgumentNullException.ThrowIfNull(services);

        services.RegisterContextType<T>(serviceKey);
    }

    /// <summary>
    /// Options-driven path: registers a scoped <see cref="DataContextBuilder"/> built by
    /// <paramref name="optionsBuilder"/> plus an <see cref="IDataContext"/> factory that turns it into a
    /// context.
    /// </summary>
    private static void RegisterContextFactory(this IServiceCollection services, object? serviceKey, Action<IServiceProvider, DataContextBuilder> optionsBuilder)
    {
        if (serviceKey is null)
        {
            services.AddScoped(sp =>
            {
                var builder = new DataContextBuilder();
                optionsBuilder(sp, builder);
                return builder;
            });

            services.AddScoped(sp => sp.GetRequiredService<DataContextBuilder>().CreateDataContext());
        }
        else
        {
            services.AddKeyedScoped(serviceKey, (sp, _) =>
            {
                var builder = new DataContextBuilder();
                optionsBuilder(sp, builder);
                return builder;
            });

            services.AddKeyedScoped(serviceKey, (sp, k) => sp.GetRequiredKeyedService<DataContextBuilder>(k).CreateDataContext());
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
