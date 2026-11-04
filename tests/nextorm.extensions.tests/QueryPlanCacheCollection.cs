using Xunit;

namespace NextORM.Extensions.Tests;

/// <summary>
/// Serializes the tests that assert a plan-cache HIT with any test that purges the process-wide plan
/// store.
/// </summary>
/// <remarks>
/// <c>QueryPlanStore</c> keeps a process-wide <c>_generation</c> while the cache it invalidates is
/// thread-local, so a <c>PurgeQueryCache()</c> on another xunit thread evicts a plan this thread just
/// stored and a warm hit turns into a miss. <see cref="CollectionDefinitionAttribute.DisableParallelization"/>
/// keeps the hit assertions (and the purges in <c>ParityTests</c>) out of each other's window, the same
/// shape the provider suites use (CI run 41846411349 hit it in
/// <c>ComponentContractTests.R3_CapturedWarmHit_ReusesThePreparedPlan</c>).
/// </remarks>
[CollectionDefinition("Query plan cache", DisableParallelization = true)]
public sealed class QueryPlanCacheCollection;
