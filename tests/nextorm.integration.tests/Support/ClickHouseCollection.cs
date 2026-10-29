namespace NextORM.Integration.Tests;

/// <summary>
/// T359 (#349): the xunit collection every ClickHouse integration test class joins.
/// </summary>
/// <remarks>
/// <para>
/// All ClickHouse test classes talk to the single server owned by <see cref="ClickHouseContainer"/>,
/// and they are serialised against it: xunit runs the tests of one collection one at a time, while
/// other providers keep running in parallel. The convention is not stylistic — two measured failure
/// modes disappear with it.
/// </para>
/// <list type="bullet">
///   <item>
///     <description>
///     <b>Duplicate query ids.</b> The driver's query ids are not unique per request, so two classes
///     issuing the same statement at the same moment make the second one fail with
///     <c>Code: 216 QUERY_WITH_SAME_ID_IS_ALREADY_RUNNING</c>. Measured on the pinned 25.8 server:
///     12 occurrences across six full runs, each paired with a concurrent start of the same
///     statement under the same id (e.g. <c>64754b74-…</c> at 15:28:22Z, finished in 3 ms for one
///     request and rejected for the other).
///     </description>
///   </item>
///   <item>
///     <description>
///     <b>Connection churn.</b> The driver opens one TCP connection per query — measured: 583
///     server-side connections for the 96-test <c>ClickHouseJoinAliasIntegrationTests</c> class —
///     so a full run issues ~6,400 connections through the WSL2 localhost relay. With eight xunit
///     threads and up to 94 query starts per second, the relay's connection table and the client's
///     4,096-port ephemeral range saturate on a loaded host, and a request waits in
///     <c>HttpConnectionPool</c> until the 120 s <c>HttpClient.Timeout</c> fires while the server
///     finishes every query in ≤5.5 s. Serialising the classes keeps the connection rate at the
///     isolated-run level (~6.5 connections per second).
///     </description>
///   </item>
/// </list>
/// <para>
/// <para>
/// Add <c>[Collection("ClickHouse")]</c> to every new ClickHouse test class. This definition exists
/// so the convention has one named home; the literal name is what the existing classes carry.
/// </para>
/// <para>
/// <b>2026-10-29 (cycle 11, step 1 attempt 2).</b> The definition additionally carries
/// <c>DisableParallelization = true</c>. Serialising the collection against <em>itself</em> was not
/// enough: a full-suite run still produced <c>Code: 216 QUERY_WITH_SAME_ID_IS_ALREADY_RUNNING</c>
/// for <c>T288NullSafeComparisonClickHouseIntegrationTests</c> and, in the baseline run, two
/// <c>HttpClient.Timeout of 120 seconds</c> failures, while every other collection kept running
/// concurrently. Nothing outside this collection executes ClickHouse statements, but the process
/// still shares the WSL2 localhost relay, the host CPU and the 2 GiB container envelope with the
/// other providers, so the collected lane is now exclusive. The flag only changes scheduling:
/// every assertion, every fixture and the container/server settings are untouched.
/// </para>
/// </remarks>
[CollectionDefinition("ClickHouse", DisableParallelization = true)]
public sealed class ClickHouseCollectionDefinition;
