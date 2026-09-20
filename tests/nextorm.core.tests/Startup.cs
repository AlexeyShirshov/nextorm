using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.DependencyInjection.Logging;

namespace NextORM.Core.Tests;

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

        services.AddScoped<EntityBuilder<ISimpleEntity>>();

        services.AddNextOrmContext<InMemoryDataContext>();

        services.AddScoped<InMemoryRepository>();
    }
}