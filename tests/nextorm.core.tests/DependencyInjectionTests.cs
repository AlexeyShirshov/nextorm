using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace NextORM.Core.Tests;

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
        services.AddNextOrmContext<InMemoryDataContext>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var viaInterface = scope.ServiceProvider.GetRequiredService<IDataContext>();
        var viaConcrete = scope.ServiceProvider.GetRequiredService<InMemoryDataContext>();

        viaInterface.Should().BeSameAs(viaConcrete);
    }

    [Fact]
    public void AddKeyedNextOrmContext_Generic_ShouldResolveTheSameInstanceForInterfaceAndConcreteType()
    {
        var services = new ServiceCollection();
        services.AddKeyedNextOrmContext<InMemoryDataContext>("key");

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var viaInterface = scope.ServiceProvider.GetRequiredKeyedService<IDataContext>("key");
        var viaConcrete = scope.ServiceProvider.GetRequiredKeyedService<InMemoryDataContext>("key");

        viaInterface.Should().BeSameAs(viaConcrete);
    }

    [Fact]
    public void AddNextOrmContext_ShouldReuseTheScopedBuilder()
    {
        var services = new ServiceCollection();
        services.AddNextOrmContext(builder => builder.Factory = _ => new InMemoryDataContext());

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
        services.AddKeyedNextOrmContext(builder => builder.Factory = _ => new InMemoryDataContext(), "key");

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredKeyedService<IDataContext>("key")
            .Should().BeOfType<InMemoryDataContext>();
    }

    [Fact]
    public void AddNextOrmContext_NullOptions_ShouldFailFast()
    {
        var services = new ServiceCollection();

        var act = () => services.AddNextOrmContext((Action<DataContextBuilder>)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // The remaining public overloads' argument guards. Their only other coverage lives in
    // nextorm.integration.tests, which is outside the Stryker test-project set, so they are pinned in
    // the core project to keep the DI/ServiceCollectionExtensions.cs mutation surface fully accounted
    // for.

    [Fact]
    public void AddNextOrmContext_ProviderOverload_NullArguments_ShouldFailFast()
    {
        var services = new ServiceCollection();

        var nullServices = () => ((IServiceCollection)null!).AddNextOrmContext((_, _) => { });
        nullServices.Should().Throw<ArgumentNullException>();

        var nullOptions = () => services.AddNextOrmContext((Action<IServiceProvider, DataContextBuilder>)null!);
        nullOptions.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddKeyedNextOrmContext_ProviderOverload_NullArguments_ShouldFailFast()
    {
        var services = new ServiceCollection();

        var nullServices = () => ((IServiceCollection)null!).AddKeyedNextOrmContext((_, _) => { }, "key");
        nullServices.Should().Throw<ArgumentNullException>();

        var nullOptions = () => services.AddKeyedNextOrmContext((Action<IServiceProvider, DataContextBuilder>)null!, "key");
        nullOptions.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddNextOrmContext_ActionOverload_NullServices_ShouldFailFast()
    {
        var nullServices = () => ((IServiceCollection)null!).AddNextOrmContext((Action<DataContextBuilder>)(_ => { }));

        nullServices.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddKeyedNextOrmContext_ActionOverload_NullArguments_ShouldFailFast()
    {
        var services = new ServiceCollection();

        var nullServices = () => ((IServiceCollection)null!).AddKeyedNextOrmContext((Action<DataContextBuilder>)(_ => { }), "key");
        nullServices.Should().Throw<ArgumentNullException>();

        var nullOptions = () => services.AddKeyedNextOrmContext((Action<DataContextBuilder>)null!, "key");
        nullOptions.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddNextOrmContext_Generic_NullServices_ShouldFailFast()
    {
        var nullServices = () => ((IServiceCollection)null!).AddNextOrmContext<InMemoryDataContext>();

        nullServices.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddKeyedNextOrmContext_Generic_NullServices_ShouldFailFast()
    {
        var nullServices = () => ((IServiceCollection)null!).AddKeyedNextOrmContext<InMemoryDataContext>("key");

        nullServices.Should().Throw<ArgumentNullException>();
    }
}
