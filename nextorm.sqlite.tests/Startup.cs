using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using nextorm.core;
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
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddXunitOutput();
            builder.AddConsole();
        });
        
        services.AddNextOrmContext<TestDataContext>((sp,builder)=>
        {
            builder.UseLoggerFactory(sp.GetRequiredService<ILoggerFactory>());
            builder.UseSqlite(@$"{Directory.GetCurrentDirectory()}\data\test.db");
            builder.LogSensetiveData(true);
        });
    }
}