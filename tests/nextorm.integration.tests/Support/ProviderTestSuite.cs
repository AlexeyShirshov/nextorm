using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Base class for every integration test class. Creates (and finally disposes) a provider
/// specific <see cref="IDataContext"/> lazily, so an unavailable database is handled before any
/// provider-specific work happens.
/// </summary>
public abstract class ProviderTestSuite : IDisposable
{
    private TestDataRepository? _repository;
    private IDataContext? _context;

    protected ProviderTestSuite()
    {
        if (Provider.IsAvailable)
            return;

        // Fail closed from the constructor: a missing container must not silently turn thousands of
        // tests into skips (a green run with 0 executed facts). Only an explicit per-provider opt-out
        // through NEXTORM_SKIP_PROVIDERS is allowed to skip; everything else is a failure.
        if (ProviderSkipPolicy.IsOptedOut(Provider.Name))
        {
            Assert.Skip(
                $"Provider '{Provider.Name}' is explicitly opted out via {ProviderSkipPolicy.EnvironmentVariable}: " +
                Provider.SkipReason);
        }
        else
        {
            Provider.IsAvailable.Should().BeTrue(
                $"provider '{Provider.Name}' is unavailable ({Provider.SkipReason}). Integration tests fail closed " +
                "so a missing database cannot look like a pass: start the container runtime and set DOCKER_HOST, " +
                $"or opt this provider out explicitly with {ProviderSkipPolicy.EnvironmentVariable}={Provider.Name}");
        }
    }

    protected abstract ITestProvider Provider { get; }

    protected TestDataRepository _sut => _repository ??= CreateRepository();

    private TestDataRepository CreateRepository()
    {
        Provider.EnsureSeeded();
        _context = Provider.CreateContext();
        return new TestDataRepository(_context);
    }

    public void Dispose() => _context?.Dispose();
}
