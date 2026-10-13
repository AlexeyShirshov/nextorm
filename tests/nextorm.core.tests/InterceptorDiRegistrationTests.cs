using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// DI-backed builder interceptor resolution (T221/#221): a type entry is resolved from the builder's
/// captured provider, once per <see cref="DataContextBuilder.CreateDataContext"/> call, with
/// first-wins deduplication, dual-interface dispatch, DI lifetimes and fail-closed error boundaries.
/// The in-memory context ignores interceptors, so these tests observe resolution directly; the SQLite
/// suite observes the actual callbacks.
/// </summary>
[Trait("Category", "T221")]
public class InterceptorDiRegistrationTests
{
    public sealed class Resolutions
    {
        public List<object> Query { get; } = [];
        public List<object> Connection { get; } = [];
        public List<object> Dual { get; } = [];
    }

    public sealed class DiQueryInterceptor : IQueryInterceptor
    {
        public DiQueryInterceptor(Resolutions resolutions) => resolutions.Query.Add(this);
    }

    public sealed class DiConnectionInterceptor : IConnectionInterceptor
    {
        public DiConnectionInterceptor(Resolutions resolutions) => resolutions.Connection.Add(this);
    }

    public sealed class DiDualInterceptor : IQueryInterceptor, IConnectionInterceptor
    {
        public DiDualInterceptor(Resolutions resolutions) => resolutions.Dual.Add(this);
    }

    public sealed class DisposableQueryInterceptor : IQueryInterceptor, IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }

    private sealed class NoopQueryInterceptor : IQueryInterceptor
    {
    }

    private sealed class NoopConnectionInterceptor : IConnectionInterceptor
    {
    }

    private readonly struct StructQueryInterceptor : IQueryInterceptor
    {
    }

    private sealed class FactoryProbe
    {
        public int Invocations;
        public List<(IQueryInterceptor[] Query, IConnectionInterceptor[] Connection)> Creations { get; } = [];

        public IDataContext Create(DataContextBuilder builder)
        {
            Invocations++;
            Creations.Add(builder.ResolveInterceptors());
            return new InMemoryDataContext();
        }
    }

    private static ServiceProvider BuildProvider(
        Action<IServiceCollection> register,
        Action<DataContextBuilder> configure)
    {
        var services = new ServiceCollection();
        services.AddSingleton<Resolutions>();
        register(services);
        services.AddNextOrmContext((_, builder) => configure(builder));
        return services.BuildServiceProvider();
    }

    [Fact]
    public void GenericOverload_ResolvesQueryInterceptorFromCapturedProvider()
    {
        var probe = new FactoryProbe();
        using var provider = BuildProvider(
            services => services.AddScoped<DiQueryInterceptor>(),
            builder =>
            {
                builder.AddInterceptor<DiQueryInterceptor>();
                builder.Factory = probe.Create;
            });
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IDataContext>();

        probe.Invocations.Should().Be(1);
        probe.Creations.Should().ContainSingle();
        probe.Creations[0].Query.Should().ContainSingle().Which.Should().BeOfType<DiQueryInterceptor>();
        probe.Creations[0].Connection.Should().BeEmpty();
    }

    [Fact]
    public void TypeOverload_ResolvesConnectionInterceptor()
    {
        var probe = new FactoryProbe();
        using var provider = BuildProvider(
            services => services.AddScoped<DiConnectionInterceptor>(),
            builder =>
            {
                builder.AddInterceptor(typeof(DiConnectionInterceptor));
                builder.Factory = probe.Create;
            });
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IDataContext>();

        probe.Creations[0].Connection.Should().ContainSingle().Which.Should().BeOfType<DiConnectionInterceptor>();
        probe.Creations[0].Query.Should().BeEmpty();
    }

    [Fact]
    public void InterfaceServiceKey_IsResolvedFromProvider()
    {
        var probe = new FactoryProbe();
        using var provider = BuildProvider(
            services => services.AddScoped<IQueryInterceptor, DiQueryInterceptor>(),
            builder =>
            {
                builder.AddInterceptor(typeof(IQueryInterceptor));
                builder.Factory = probe.Create;
            });
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IDataContext>();

        probe.Creations[0].Query.Should().ContainSingle().Which.Should().BeOfType<DiQueryInterceptor>();
    }

    [Fact]
    public void BoxedStructInstance_IsRetained()
    {
        var probe = new FactoryProbe();
        var interceptor = (IQueryInterceptor)new StructQueryInterceptor();
        var builder = new DataContextBuilder().AddInterceptor(interceptor);
        builder.Factory = probe.Create;

        builder.CreateDataContext();

        probe.Creations[0].Query.Should().ContainSingle().Which.Should().BeSameAs(interceptor);
    }

    [Fact]
    public void MissingRegistration_FailsBeforeTheFactoryIsInvoked()
    {
        var probe = new FactoryProbe();
        using var provider = BuildProvider(
            _ => { },
            builder =>
            {
                builder.AddInterceptor<DiQueryInterceptor>();
                builder.Factory = probe.Create;
            });
        using var scope = provider.CreateScope();

        var act = () => scope.ServiceProvider.GetRequiredService<IDataContext>();

        act.Should().Throw<InvalidOperationException>();
        probe.Invocations.Should().Be(0, "resolution must complete (and fail) before the context factory runs");
    }

    [Fact]
    public void StandaloneTypeEntry_FailsBeforeTheFactoryIsInvoked()
    {
        var probe = new FactoryProbe();
        var builder = new DataContextBuilder().AddInterceptor<DiQueryInterceptor>();
        builder.Factory = probe.Create;

        var act = () => builder.CreateDataContext();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*requires a DI-backed DataContextBuilder*");
        probe.Invocations.Should().Be(0);
    }

    [Fact]
    public void MixedInstanceAndType_KeepRegistrationOrderAndDispatchPerInterface()
    {
        var first = new NoopQueryInterceptor();
        var second = new NoopConnectionInterceptor();
        var probe = new FactoryProbe();
        using var provider = BuildProvider(
            services => services.AddScoped<DiDualInterceptor>(),
            builder =>
            {
                builder
                    .AddInterceptor(first)
                    .AddInterceptor<DiDualInterceptor>()
                    .AddInterceptor(second);
                builder.Factory = probe.Create;
            });
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IDataContext>();

        var creation = probe.Creations[0];
        creation.Query.Should().HaveCount(2);
        creation.Query[0].Should().BeSameAs(first);
        creation.Query[1].Should().BeOfType<DiDualInterceptor>();
        // The dual-interface entry is attached to both pipelines; the connection-only instance is last.
        creation.Connection.Should().HaveCount(2);
        creation.Connection[0].Should().BeOfType<DiDualInterceptor>();
        creation.Connection[1].Should().BeSameAs(second);
    }

    [Fact]
    public void DuplicateType_IsResolvedOnlyOnce()
    {
        var probe = new FactoryProbe();
        using var provider = BuildProvider(
            services => services.AddScoped<DiQueryInterceptor>(),
            builder =>
            {
                builder
                    .AddInterceptor<DiQueryInterceptor>()
                    .AddInterceptor<DiQueryInterceptor>();
                builder.Factory = probe.Create;
            });
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IDataContext>();

        scope.ServiceProvider.GetRequiredService<Resolutions>().Query.Should().ContainSingle();
        probe.Creations[0].Query.Should().ContainSingle();
    }

    [Fact]
    public void MixedDuplicate_TypeAfterInstanceOfTheSameRuntimeType_IsIgnored()
    {
        var resolutions = new Resolutions();
        var instance = new DiQueryInterceptor(resolutions);
        var probe = new FactoryProbe();
        using var provider = BuildProvider(
            services => services.AddScoped<DiQueryInterceptor>(),
            builder =>
            {
                builder
                    .AddInterceptor(instance)
                    .AddInterceptor<DiQueryInterceptor>();
                builder.Factory = probe.Create;
            });
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IDataContext>();

        resolutions.Query.Should().ContainSingle("the instance registered first wins over the type entry");
        probe.Creations[0].Query.Should().ContainSingle().Which.Should().BeSameAs(instance);
    }

    [Fact]
    public void DistinctAliases_AreNotMergedByTheirRuntimeImplementation()
    {
        var probe = new FactoryProbe();
        using var provider = BuildProvider(
            services =>
            {
                services.AddScoped<DiQueryInterceptor>();
                services.AddScoped<IQueryInterceptor>(sp => sp.GetRequiredService<DiQueryInterceptor>());
            },
            builder =>
            {
                builder
                    .AddInterceptor<IQueryInterceptor>()
                    .AddInterceptor<DiQueryInterceptor>();
                builder.Factory = probe.Create;
            });
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IDataContext>();

        probe.Creations[0].Query.Should().HaveCount(2, "an interface key and the concrete key are distinct registrations");
    }

    [Fact]
    public void DualInterfaceEntry_IsAttachedToBothPipelinesAndResolvedOnce()
    {
        var probe = new FactoryProbe();
        using var provider = BuildProvider(
            services => services.AddScoped<DiDualInterceptor>(),
            builder =>
            {
                builder.AddInterceptor<DiDualInterceptor>();
                builder.Factory = probe.Create;
            });
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IDataContext>();

        scope.ServiceProvider.GetRequiredService<Resolutions>().Dual.Should().ContainSingle();
        probe.Creations[0].Query.Should().ContainSingle();
        probe.Creations[0].Connection.Should().ContainSingle();
        probe.Creations[0].Query[0].Should().BeSameAs(probe.Creations[0].Connection[0]);
    }

    [Fact]
    public void ScopedLifetime_IsReusedWithinAScopeAndDiffersAcrossScopes()
    {
        using var provider = BuildProvider(
            services => services.AddScoped<DiQueryInterceptor>(),
            builder =>
            {
                builder.AddInterceptor<DiQueryInterceptor>();
                builder.Factory = _ => new InMemoryDataContext();
            });

        using var first = provider.CreateScope();
        using var second = provider.CreateScope();

        first.ServiceProvider.GetRequiredService<IDataContext>();
        var scopedFromFirst = first.ServiceProvider.GetRequiredService<DiQueryInterceptor>();
        var eagerFromFirst = first.ServiceProvider.GetRequiredService<DiQueryInterceptor>();
        first.ServiceProvider.GetRequiredService<IDataContext>();

        second.ServiceProvider.GetRequiredService<IDataContext>();
        var scopedFromSecond = second.ServiceProvider.GetRequiredService<DiQueryInterceptor>();

        scopedFromFirst.Should().BeSameAs(eagerFromFirst);
        scopedFromSecond.Should().NotBeSameAs(scopedFromFirst);
    }

    [Fact]
    public void SingletonLifetime_IsSharedAcrossScopes()
    {
        var probe = new FactoryProbe();
        using var provider = BuildProvider(
            services => services.AddSingleton<DiQueryInterceptor>(),
            builder =>
            {
                builder.AddInterceptor<DiQueryInterceptor>();
                builder.Factory = probe.Create;
            });

        using var first = provider.CreateScope();
        using var second = provider.CreateScope();

        first.ServiceProvider.GetRequiredService<IDataContext>();
        second.ServiceProvider.GetRequiredService<IDataContext>();

        probe.Creations[0].Query[0].Should().BeSameAs(probe.Creations[1].Query[0]);
    }

    [Fact]
    public void TransientLifetime_IsResolvedPerCreation()
    {
        var probe = new FactoryProbe();
        using var provider = BuildProvider(
            services => services.AddTransient<DiQueryInterceptor>(),
            builder =>
            {
                builder.AddInterceptor<DiQueryInterceptor>();
                builder.Factory = probe.Create;
            });
        using var scope = provider.CreateScope();
        var builder = scope.ServiceProvider.GetRequiredService<DataContextBuilder>();

        builder.CreateDataContext();
        builder.CreateDataContext();

        probe.Creations.Should().HaveCount(2);
        probe.Creations[0].Query[0].Should().NotBeSameAs(probe.Creations[1].Query[0]);
    }

    [Fact]
    public void Disposal_IsOwnedByTheServiceProvider()
    {
        DisposableQueryInterceptor? interceptor;
        using (var provider = BuildProvider(
            services => services.AddScoped<DisposableQueryInterceptor>(),
            builder =>
            {
                builder.AddInterceptor<DisposableQueryInterceptor>();
                builder.Factory = _ => new InMemoryDataContext();
            }))
        {
            using (var scope = provider.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<IDataContext>();
                interceptor = scope.ServiceProvider.GetRequiredService<DisposableQueryInterceptor>();
                interceptor.Disposed.Should().BeFalse("the builder never disposes a DI-owned interceptor");
            }

            interceptor.Disposed.Should().BeTrue("disposing the scope releases the DI-owned interceptor");
        }
    }

    [Fact]
    public void KeyedBuilderRoute_CapturesTheScopeProvider()
    {
        var probe = new FactoryProbe();
        var services = new ServiceCollection();
        services.AddSingleton<Resolutions>();
        services.AddScoped<DiQueryInterceptor>();
        services.AddKeyedNextOrmContext(
            (_, builder) =>
            {
                builder.AddInterceptor<DiQueryInterceptor>();
                builder.Factory = probe.Create;
            },
            "primary");
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredKeyedService<IDataContext>("primary");

        probe.Creations[0].Query.Should().ContainSingle();
    }

    [Fact]
    public void OrdinaryRouteWithoutInterceptors_CreatesContextsRepeatedly()
    {
        var probe = new FactoryProbe();
        using var provider = BuildProvider(
            _ => { },
            builder => builder.Factory = probe.Create);
        using var scope = provider.CreateScope();
        var builder = scope.ServiceProvider.GetRequiredService<DataContextBuilder>();

        builder.CreateDataContext();
        builder.CreateDataContext();

        probe.Invocations.Should().Be(2);
        probe.Creations[0].Query.Should().BeEmpty();
        probe.Creations[0].Connection.Should().BeEmpty();
    }
}
