using NextORM.Core;

namespace NextORM.EntityFrameworkCore.Tests;

/// <summary>
/// Clears nextorm's process-wide metadata after every test, so a mapping registered by one test (or a
/// model-shape refusal) cannot leak into the next test.
/// </summary>
public abstract class EfCoreMetadataCleanup : IDisposable
{
    public void Dispose() => DataContextCache.Clear();
}
