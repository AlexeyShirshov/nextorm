namespace NextORM.Sqlite.Tests;

/// <summary>
/// Serializes the tests that assert the process-wide plan store's contents or a plan-cache HIT with any
/// test that purges it.
/// </summary>
/// <remarks>
/// <c>QueryPlanStore</c> keeps a process-wide <c>_generation</c> while the cache it invalidates is
/// thread-local, so a <c>PurgeQueryCache()</c> on another xunit thread silently evicts a plan this
/// thread just stored. A hit/miss or entry-count assertion is only valid while nothing else in the
/// process purges, which <see cref="CollectionDefinitionAttribute.DisableParallelization"/> guarantees
/// (the same shape as <c>DataContextCache clear</c> and the provider suites' <c>Query plan cache</c>).
/// Observed flake: CI run 41840157412 measured <c>0 -&gt; 1 -&gt; 2 -&gt; 2</c> plan entries where the
/// statement identities require <c>0 -&gt; 1 -&gt; 2 -&gt; 4</c>.
/// </remarks>
[CollectionDefinition("Query plan cache", DisableParallelization = true)]
public sealed class QueryPlanCacheCollection;
