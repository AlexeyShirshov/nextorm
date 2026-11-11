namespace NextORM.Integration.Tests;

/// <summary>
/// Serialises the prepared-command identity tests against every other integration collection: a purge executed by
/// any concurrently scheduled class clears the process-wide plan store and the thread-local plan dictionaries, which
/// invalidates the prepared-command identity these tests assert by reference.
/// </summary>
[CollectionDefinition("Query plan cache", DisableParallelization = true)]
public sealed class QueryPlanCacheCollection;
