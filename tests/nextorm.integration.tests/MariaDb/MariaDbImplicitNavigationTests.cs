using FluentAssertions;
using NextORM.Core;
using NextORM.MariaDb;

namespace NextORM.Integration.Tests;

/// <summary>
/// #148-B D8 / A12: MariaDB does not derive <see cref="CommonTestSuite"/>, so this feature suite runs
/// the implicit-navigation conformance scenarios against a real MariaDB server (a Testcontainers
/// instance unless <c>NEXTORM_MARIADB_CONNECTION</c> points at an existing server). It must never be
/// silently skipped: when the container is unavailable the constructor reports the reason.
/// </summary>
public sealed class MariaDbImplicitNavigationTests : IDisposable
{
    private static readonly object SchemaGate = new();
    private static bool _schemaReady;

    private readonly IDataContext _ctx;

    public MariaDbImplicitNavigationTests()
    {
        Assert.SkipUnless(MariaDbContainer.IsAvailable, MariaDbContainer.Failure ?? "MariaDB is not available.");

        _ctx = new MariaDbDataContext(MariaDbContainer.ConnectionString, new DataContextBuilder());
        EnsureSchema();
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void Reference_scalar_chain_and_presence()
        => NavConScenarios.ReferenceScalarChainAndPresence(_ctx);

    [Fact]
    public void Reference_whole_projection_and_dangling()
        => NavConScenarios.ReferenceWholeProjectionAndDangling(_ctx);

    [Fact]
    public void Reference_lifted_coalesced_and_unlifted_guard()
        => NavConScenarios.ReferenceLiftedCoalescedAndUnliftedGuard(_ctx);

    [Fact]
    public void One_to_many_terminals()
        => NavConScenarios.CollectionTerminalsOneToMany(_ctx);

    [Fact]
    public void Many_to_many_terminals()
        => NavConScenarios.CollectionTerminalsManyToMany(_ctx);

    [Fact]
    public void Collection_adapter_matches_direct_terminals()
        => NavConScenarios.AdapterEquivalentToDirectTerminals(_ctx);

    [Fact]
    public void Reject_list_fails_closed()
        => NavConScenarios.RejectList(_ctx);

    [Fact]
    public void Captured_enumerable_is_not_navigation()
        => NavConScenarios.CapturedReceiverIsNotNavigation(_ctx);

    [Fact]
    public void Same_type_paths_keep_distinct_aliases()
        => NavConScenarios.AliasIdentitySameTypePaths(_ctx);

    [Fact]
    public void Cold_warm_prepared_and_shared_command_hygiene()
        => NavConScenarios.ColdWarmPreparedAndSharedCommandHygiene(_ctx);

    [Fact]
    public void Multi_hop_reference_presence()
        => NavConScenarios.MultiHopReferencePresence(_ctx);

    [Fact]
    public void Collection_through_a_reference()
        => NavConScenarios.CollectionThroughAReference(_ctx);

    [Fact]
    public void Reference_adapter_matches_explicit_reference_semantics()
        => NavConScenarios.ReferenceAdapterEquivalentToExplicit(_ctx);

    [Fact]
    public void Wide_count_boundary_stays_in_range_and_wide()
        => NavConScenarios.WideCountInRangeBoundary(_ctx);

    private void EnsureSchema()
    {
        if (_schemaReady)
            return;

        lock (SchemaGate)
        {
            if (_schemaReady)
                return;

            foreach (var statement in SchemaStatements)
                Execute(statement);

            _schemaReady = true;
        }
    }

    private void Execute(string sql)
    {
        ((DataContext)_ctx).EnsureConnectionOpen();
        using var cmd = ((DataContext)_ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }

    // Same shape as the MySQL common provider's eager_* fixtures.
    private static readonly string[] SchemaStatements =
    [
        "drop table if exists eager_link",
        "drop table if exists eager_tag",
        "drop table if exists eager_child",
        "drop table if exists eager_parent",
        "create table eager_parent (id int not null primary key, name varchar(100) null)",
        "create table eager_child (id int not null primary key, parent_id int not null, name varchar(100) null)",
        "create table eager_tag (id int not null primary key, name varchar(100) null)",
        "create table eager_link (id int not null primary key, parent_id int not null, child_id int not null)",
    ];
}
