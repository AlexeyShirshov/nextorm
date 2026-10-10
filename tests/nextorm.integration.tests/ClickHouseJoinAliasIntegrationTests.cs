using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Issue #160 (R160-09) real-DB coverage for join aliases and free positional/alias mixing on
/// ClickHouse. ClickHouse has no <see cref="CommonTestSuite"/>-derived integration class (it runs
/// against its own <see cref="ClickHouseContainer"/> harness), so the shared
/// <see cref="CommonTestSuite"/> join-alias bodies are re-pinned here against a real ClickHouse
/// server. ClickHouse supports the five explicit join operators but has no lateral source, so no
/// CROSS/OUTER APPLY body is re-pinned (the fail-closed guard has its own SQL-generation tests). A
/// skip is never a pass: when the container is unavailable the constructor reports the reason.
/// </summary>
public sealed class ClickHouseJoinAliasIntegrationTests : IDisposable
{
    private readonly IDataContext _ctx;
    private readonly TestDataRepository _sut;

    public ClickHouseJoinAliasIntegrationTests()
    {
        Assert.SkipUnless(ClickHouseContainer.IsAvailable, ClickHouseContainer.Failure ?? "ClickHouse is not available.");

        _ctx = new ClickHouseDataContext(ClickHouseContainer.ConnectionString, new DataContextBuilder());
        Seed();
        _sut = new TestDataRepository(_ctx);
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void Alias_inner_join_returns_matched_rows()
        => CommonTestSuite.AliasInnerJoinReturnsMatchedRows(_sut);

    [Fact]
    public void Alias_left_join_returns_all_orders()
        => CommonTestSuite.AliasLeftJoinReturnsAllOrders(_sut);

    [Fact]
    public void Alias_right_join_keeps_unmatched_right_rows()
        => CommonTestSuite.AliasRightJoinKeepsUnmatchedRightRows(_sut);

    // ClickHouse has a native FULL JOIN, so the shared positive branch runs against the real server.
    [Fact]
    public void Alias_full_join_returns_both_sides()
        => CommonTestSuite.AliasFullJoinReturnsBothSidesOrFailsClosed(_sut, supportsFullJoin: true);

    [Fact]
    public void Alias_cross_join_returns_cartesian_product()
        => CommonTestSuite.AliasCrossJoinReturnsCartesianProduct(_sut);

    [Fact]
    public void Alias_buyer_and_approver_resolve_to_distinct_ids()
        => CommonTestSuite.AliasBuyerAndApproverResolveToDistinctIds(_sut);

    [Fact]
    public void Alias_join_across_a_method_boundary_resolves_both_slots()
        => CommonTestSuite.AliasJoinAcrossMethodBoundaryResolvesBothSlots(_sut);

    [Fact]
    public void Root_alias_inner_join_returns_matched_rows()
        => CommonTestSuite.RootAliasInnerJoinReturnsMatchedRows(_sut);

    [Fact]
    public void Root_alias_item1_and_root_name_resolve_to_the_same_slot()
        => CommonTestSuite.RootAliasItem1AndRootNameResolveToSameSlot(_sut);

    [Fact]
    public void Root_alias_supports_a_positional_join_after_it()
        => CommonTestSuite.RootAliasSupportsPositionalJoinAfterIt(_sut);

    [Fact]
    public void Mixed_alias_then_positional_join_executes_and_keeps_slot_identity()
        => CommonTestSuite.MixedAliasThenPositionalJoinExecutesAndKeepsSlotIdentity(_sut);

    [Fact]
    public void Mixed_positional_then_alias_join_executes_and_keeps_slot_identity()
        => CommonTestSuite.MixedPositionalThenAliasJoinExecutesAndKeepsSlotIdentity(_sut);

    // Same schema and rows as the shared provider matrix (MySqlTestProvider/Postgres/SqlServer/SQLite),
    // kept local because ClickHouse does not share the provider seeding matrix.
    private void Seed()
    {
        Execute("drop table if exists orders");
        Execute("drop table if exists person");
        Execute("create table person (id Int32, name Nullable(String)) engine = Memory");
        Execute("insert into person (id, name) values (10, 'Buyer'), (20, 'Approver'), (30, 'Unlinked')");
        Execute("create table orders (id Int32, buyer_id Int32, approver_id Int32) engine = Memory");
        Execute("insert into orders (id, buyer_id, approver_id) values (1, 10, 20), (2, 20, 10)");
    }

    private void Execute(string sql)
    {
        ((DataContext)_ctx).EnsureConnectionOpen();
        using var cmd = ((DataContext)_ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }
}
