using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
using Xunit.DependencyInjection;
using Xunit.DependencyInjection.Logging;

namespace nextorm.core.tests;

public class Startup
{
    public void Configure()
    {
        
    }
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder=>builder.AddXunitOutput());

        services.AddScoped<SqlClient>();

        services.AddScoped<CommandBuilder<ISimpleEntity>>();
    }
}