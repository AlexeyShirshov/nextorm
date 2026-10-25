namespace NextORM.Integration.Tests;

/// <summary>
/// Provider agnostic integration tests. The facts are split across partial files (mirroring the
/// original test files) and are executed once per concrete provider subclass.
/// <para>
/// The base type is the prepared-command cache suite (<see cref="PreparedCommandCacheSuite"/>), which
/// derives from <see cref="ProviderTestSuite"/>; it carries the T333 prepared-command facts so every
/// provider that uses the shared suite inherits them, and its ClickHouse subclass reuses the same
/// facts (ClickHouse's own integration class deliberately derives from <see cref="ProviderTestSuite"/>).
/// </para>
/// </summary>
public abstract partial class CommonTestSuite : PreparedCommandCacheSuite
{
}
