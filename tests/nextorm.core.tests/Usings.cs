global using Xunit;
using Xunit.Sdk;
using Xunit.v3;

[assembly: Parallelization(Mode = ParallelMode.None)]
[assembly: AssemblyFixture(typeof(NextORM.Core.Tests.ProcessWideCacheIsolationFixture))]
[assembly: TestCollectionOrderer(typeof(NextORM.Core.Tests.T342CollectionOrderer))]