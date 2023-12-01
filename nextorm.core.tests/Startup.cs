using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.DependencyInjection.Logging;

namespace nextorm.core.tests;

public class Startup
{
    public void Configure()
    {

    }
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddXunitOutput();
            builder.AddConsole();
        });

        services.AddScoped<Entity<ISimpleEntity>>();

        services.AddNextOrmContext<InMemoryContext>();

        services.AddScoped<InMemoryRepository>();
    }
}