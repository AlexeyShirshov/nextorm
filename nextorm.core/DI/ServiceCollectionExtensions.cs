using Microsoft.Extensions.DependencyInjection;

namespace nextorm.core;

public static class ServiceCollectionExtensions
{
    public static void AddNextOrmContext<T>(this IServiceCollection services, Action<IServiceProvider, DataContextOptionsBuilder>? optionsBuilder)
        where T : class, IDataContext
    {
        if (optionsBuilder is not null)
        {
            services.AddScoped(sp =>
            {
                var builder = new DataContextOptionsBuilder();
                optionsBuilder(sp, builder);
                return builder;
            });
        }

        services.AddScoped<IDataContext, T>();
        services.AddScoped<T>();
    }
    public static void AddNextOrmContext<T>(this IServiceCollection services, Action<DataContextOptionsBuilder>? optionsBuilder)
        where T : class, IDataContext
    {
        if (optionsBuilder is not null)
        {
            //services.Configure(optionsBuilder);
            services.AddScoped(_ =>
            {
                var builder = new DataContextOptionsBuilder();
                optionsBuilder(builder);
                return builder;
            });
        }

        services.AddScoped<IDataContext, T>();
        services.AddScoped<T>();
    }
    public static void AddNextOrmContext<T>(this IServiceCollection services)
        where T : class, IDataContext
    {
        services.AddScoped<IDataContext, T>();
        services.AddScoped<T>();
    }
}