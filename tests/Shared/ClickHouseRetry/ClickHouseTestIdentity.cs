using Xunit;

namespace NextORM.Tests.Shared.ClickHouseRetry;

/// <summary>
/// The one identity a retry-controlled probe and the runner reporter agree on (T328 AC05). The
/// reporter learns each test's identity from <c>ITestStarting.TestDisplayName</c>; a probe that
/// records anything else (a class name, a constant or an operation label) can never be correlated
/// with the runner's final outcome. This helper therefore returns exactly that display name, and
/// where xUnit cannot supply it, returns an explicitly non-correlatable sentinel instead of a
/// plausible-looking substitute.
/// </summary>
internal static class ClickHouseTestIdentity
{
    /// <summary>
    /// Prefix of every fallback identity. A value carrying it is never the runner's display name, so
    /// a missing correlation is visible instead of being mistaken for a real one.
    /// </summary>
    internal const string UnavailablePrefix = "unavailable:";

    /// <summary>
    /// The current test's display name, identical to <c>ITestStarting.TestDisplayName</c>, which the
    /// reporter keys on. Never throws: outside a test body, or when the runner exposes no test
    /// metadata, the result is <see cref="UnavailablePrefix"/> followed by this helper's
    /// assembly-qualified name.
    /// </summary>
    internal static string CurrentTestDisplayName()
    {
        try
        {
            var displayName = TestContext.Current.Test?.TestDisplayName;
            return string.IsNullOrWhiteSpace(displayName)
                ? Unavailable()
                : displayName;
        }
        catch (Exception)
        {
            // Identity discovery must never fail a test body; the fallback records why it is absent.
            return Unavailable();
        }
    }

    /// <summary>Whether <paramref name="identity"/> came from the runner rather than the fallback.</summary>
    internal static bool IsCorrelatable(string identity) =>
        !string.IsNullOrWhiteSpace(identity)
        && !identity.StartsWith(UnavailablePrefix, StringComparison.Ordinal);

    private static string Unavailable() =>
        UnavailablePrefix
        + (typeof(ClickHouseTestIdentity).AssemblyQualifiedName
           ?? typeof(ClickHouseTestIdentity).FullName
           ?? nameof(ClickHouseTestIdentity));
}
