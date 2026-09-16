using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace nextorm.core.tests;

/// <summary>
/// F8: DI registration must produce a single context instance per scope and must fail fast when the
/// options delegate is missing.
/// </summary>
public class DependencyInjectionTests
{
    [Fact]
    public void AddNextOrmContext_Generic_ShouldResolveTheSameInstanceForInterfaceAndConcreteType()
    {
        var services = new ServiceCollection();
        services.AddNextOrmContext<InMemoryContext>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var viaInterface = scope.ServiceProvider.GetRequiredService<IDataContext>();
        var viaConcrete = scope.ServiceProvider.GetRequiredService<InMemoryContext>();

        viaInterface.Should().BeSameAs(viaConcrete);
    }

    [Fact]
    public void AddKeyedNextOrmContext_Generic_ShouldResolveTheSameInstanceForInterfaceAndConcreteType()
    {
        var services = new ServiceCollection();
        services.AddKeyedNextOrmContext<InMemoryContext>("key");

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var viaInterface = scope.ServiceProvider.GetRequiredKeyedService<IDataContext>("key");
        var viaConcrete = scope.ServiceProvider.GetRequiredKeyedService<InMemoryContext>("key");

        viaInterface.Should().BeSameAs(viaConcrete);
    }

    [Fact]
    public void AddNextOrmContext_ShouldReuseTheScopedBuilder()
    {
        var services = new ServiceCollection();
        services.AddNextOrmContext(builder => builder.Factory = _ => new InMemoryContext());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var first = scope.ServiceProvider.GetRequiredService<IDataContext>();
        var second = scope.ServiceProvider.GetRequiredService<IDataContext>();

        first.Should().BeSameAs(second);
    }

    [Fact]
    public void AddKeyedNextOrmContext_ShouldResolveTheKeyedContext()
    {
        var services = new ServiceCollection();
        services.AddKeyedNextOrmContext(builder => builder.Factory = _ => new InMemoryContext(), "key");

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredKeyedService<IDataContext>("key")
            .Should().BeOfType<InMemoryContext>();
    }

    [Fact]
    public void AddNextOrmContext_NullOptions_ShouldFailFast()
    {
        var services = new ServiceCollection();

        var act = () => services.AddNextOrmContext((Action<DbContextBuilder>)null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
