using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
using Xunit.DependencyInjection;
using Xunit.DependencyInjection.Logging;

namespace nextorm.sqlite.tests;

public class Startup
{
    public void Configure()
    {
    }
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder=>
        {
            builder.AddXunitOutput();
            builder.AddConsole();
        });
        
        services.AddScoped<DataContext>();
    }
}