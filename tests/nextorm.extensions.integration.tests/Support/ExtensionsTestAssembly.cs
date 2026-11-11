using NextORM.Integration.Tests;
using Xunit;

// The provider containers are owned by the integration harness; this assembly needs its own fixture
// registration so the run disposes them, exactly like the integration project does.
[assembly: AssemblyFixture(typeof(DatabaseContainers))]

namespace NextORM.Integration.Tests;

/// <summary>Serialises the extensions live facts with every other collection of this assembly.</summary>
[CollectionDefinition("ClickHouse", DisableParallelization = true)]
public sealed class ClickHouseCollectionDefinition;

/// <summary>Serialises the extensions live facts with every other collection of this assembly.</summary>
[CollectionDefinition("Query plan cache", DisableParallelization = true)]
public sealed class QueryPlanCacheCollection;
