using nextorm.core;

namespace nextorm.integration.tests;

/// <summary>
/// Base class for every integration test class. Creates (and finally disposes) a provider
/// specific <see cref="IDataContext"/> lazily, so the availability check can skip the test
/// from inside the test body when the database has not been configured.
/// </summary>
public abstract class ProviderTestSuite : IDisposable
{
    private TestDataRepository? _repository;
    private IDataContext? _context;

    protected ProviderTestSuite()
    {
        // Dynamic skip from the constructor so every test of an unavailable provider is reported
        // as skipped, even the ones that only touch the context from inside an assertion lambda.
        Assert.SkipUnless(Provider.IsAvailable, Provider.SkipReason);
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
