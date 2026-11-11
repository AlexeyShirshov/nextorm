using Xunit;
using Xunit.v3;

namespace NextORM.Core.Tests;

/// <summary>
/// T342 (#342) assembly-wide isolation of the process-wide <see cref="DataContextCache"/> state.
/// A single assembly fixture replaces per-class teardown wiring: after <b>every</b> test the runner
/// calls <see cref="OnTestFinished(IXunitTest)"/>, which restores the sliding-expiration window
/// captured once at assembly start and clears the process-wide caches, so no test can hand a
/// contaminated cache to the next one.
/// </summary>
/// <remarks>
/// <para>
/// The fixture deliberately performs <b>no</b> cleanup when a test starts: an entry leaked by a
/// preceding test must stay observable to the test that inherits it (the T342 reproduction relies on
/// exactly that), and is removed only when its owner hands control back.
/// </para>
/// <para>
/// <see cref="Xunit.AssemblyFixtureAttribute"/> requires a public parameterless constructor; the
/// fixture is <c>sealed</c> by repository convention. Both lifecycle notifications are the
/// non-async <see cref="INotifyTestLifecycle"/> members — the asynchronous mirror
/// (<c>INotifyTestLifecycleAsync</c>) is intentionally not implemented, so there is exactly one
/// teardown path and it cannot run twice per test.
/// </para>
/// </remarks>
public sealed class ProcessWideCacheIsolationFixture : INotifyTestLifecycle
{
    private static int _finishCount;
    private static long _lastRestoredWindowTicks;

    private readonly TimeSpan _assemblyWindow;

    /// <summary>
    /// Captures the sliding-expiration window once, when the assembly fixture is created (assembly
    /// start; expected <see cref="TimeSpan.Zero"/>), so every teardown can restore it.
    /// </summary>
    public ProcessWideCacheIsolationFixture()
    {
        _assemblyWindow = DataContextCache.CacheSlidingExpiration;
        Interlocked.Exchange(ref _lastRestoredWindowTicks, _assemblyWindow.Ticks);
    }

    /// <summary>Number of test-finish callbacks executed so far (read-only control observable).</summary>
    public static int FinishCount => Volatile.Read(ref _finishCount);

    /// <summary>The window the last test-finish callback restored (read-only control observable).</summary>
    public static TimeSpan LastRestoredWindow => TimeSpan.FromTicks(Interlocked.Read(ref _lastRestoredWindowTicks));

    /// <inheritdoc />
    public void OnTestStarting(IXunitTest test) => Start(Describe(test));

    /// <inheritdoc />
    public void OnTestFinished(IXunitTest test) => Finish(Describe(test));

    /// <summary>
    /// Test starting notification body. Exposed so the fixture control can run exactly what the runner
    /// runs without fabricating an <see cref="IXunitTest"/>.
    /// </summary>
    /// <param name="testName">Diagnostic name of the starting test.</param>
    public void Start(string testName) =>
        TestContext.Current.SendDiagnosticMessage($"T342 fixture: start {testName}");

    /// <summary>
    /// Test finished notification body: restores the window captured at assembly start and clears the
    /// five process-wide caches (plus the related stores <see cref="DataContextCache.Clear"/> owns).
    /// Exposed so the fixture control can run exactly what the runner runs.
    /// </summary>
    /// <param name="testName">Diagnostic name of the finished test.</param>
    public void Finish(string testName)
    {
        TestContext.Current.SendDiagnosticMessage($"T342 fixture: finish {testName}");
        DataContextCache.CacheSlidingExpiration = _assemblyWindow;
        DataContextCache.Clear();
        Interlocked.Exchange(ref _lastRestoredWindowTicks, _assemblyWindow.Ticks);
        Interlocked.Increment(ref _finishCount);
    }

    private static string Describe(IXunitTest test)
    {
        var method = test.TestMethod.Method;
        return $"{method.DeclaringType?.Name}.{method.Name}";
    }
}
