namespace nextorm.integration.tests;

/// <summary>
/// Provider agnostic integration tests. The facts are split across partial files (mirroring the
/// original test files) and are executed once per concrete provider subclass.
/// </summary>
public abstract partial class CommonTestSuite : ProviderTestSuite
{
}
