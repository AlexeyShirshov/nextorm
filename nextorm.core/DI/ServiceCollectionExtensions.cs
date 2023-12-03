using Microsoft.Extensions.DependencyInjection;

namespace nextorm.core;

public static class ServiceCollectionExtensions
{
    public static void AddNextOrmContext(this IServiceCollection services, Action<IServiceProvider, DbContextBuilder>? optionsBuilder)
    {
        if (optionsBuilder is not null)
        {
            services.AddScoped(sp =>
            {
                var builder = new DbContextBuilder();
                optionsBuilder(sp, builder);
                return builder;
            });
        }

        services.AddScoped(sp =>
        {
            var builder = sp.GetRequiredService<DbContextBuilder>();
            return builder.CreateDbContext();
        });
    }
    public static void AddKeyedNextOrmContext(this IServiceCollection services, Action<IServiceProvider, DbContextBuilder>? optionsBuilder, object? serviceKey)
    {
        if (optionsBuilder is not null)
        {
            services.AddKeyedScoped(serviceKey, (sp, k) =>
            {
                var builder = new DbContextBuilder();
                optionsBuilder(sp, builder);
                return builder;
            });
        }

        services.AddKeyedScoped(serviceKey, (sp, k) =>
        {
            var builder = sp.GetRequiredKeyedService<DbContextBuilder>(k);
            return builder.CreateDbContext();
        });
    }
    public static void AddNextOrmContext(this IServiceCollection services, Action<DbContextBuilder>? optionsBuilder)
    {
        if (optionsBuilder is not null)
        {
            //services.Configure(optionsBuilder);
            services.AddScoped(_ =>
            {
                var builder = new DbContextBuilder();
                optionsBuilder(builder);
                return builder;
            });
        }

        services.AddScoped(sp =>
        {
            var builder = sp.GetRequiredService<DbContextBuilder>();
            return builder.CreateDbContext();
        });
    }
    public static void AddKeyedNextOrmContext(this IServiceCollection services, Action<DbContextBuilder>? optionsBuilder, object? serviceKey)
    {
        if (optionsBuilder is not null)
        {
            //services.Configure(optionsBuilder);
            services.AddKeyedScoped(serviceKey, (_, _) =>
            {
                var builder = new DbContextBuilder();
                optionsBuilder(builder);
                return builder;
            });
        }

        services.AddKeyedScoped(serviceKey, (sp, k) =>
        {
            var builder = sp.GetRequiredKeyedService<DbContextBuilder>(k);
            return builder.CreateDbContext();
        });
    }
    public static void AddNextOrmContext<T>(this IServiceCollection services)
        where T : class, IDataContext
    {
        services.AddScoped<IDataContext, T>();
        services.AddScoped<T>();
    }
    public static void AddKeyedNextOrmContext<T>(this IServiceCollection services, object? serviceKey)
        where T : class, IDataContext
    {
        services.AddKeyedScoped<IDataContext, T>(serviceKey);
        services.AddKeyedScoped<T>(serviceKey);
    }
}