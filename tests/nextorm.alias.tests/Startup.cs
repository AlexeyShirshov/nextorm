using Microsoft.Extensions.DependencyInjection;

namespace NextORM.AliasTests;

/// <summary>
/// Minimal <c>Xunit.DependencyInjection</c> startup. The alias suite needs no injected services, but
/// the shared test package set (<c>Xunit.DependencyInjection.Logging</c>) expects a startup type.
/// </summary>
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
    }
}
