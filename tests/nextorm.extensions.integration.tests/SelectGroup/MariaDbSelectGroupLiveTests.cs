using FluentAssertions;
using MySqlConnector;
using NextORM.Core;
using NextORM.MariaDb;

namespace NextORM.Integration.Tests;

/// <summary>
/// MariaDB runs the same exact-value oracles as the <see cref="SelectGroupLiveTestsBase"/> providers.
/// It keeps its own seeding because MariaDB is not one of the <c>ITestProvider</c> container-backed
/// suites (<c>MariaDbContainer</c> only), but the expected values come from the one shared
/// <see cref="SelectGroupLiveOracles"/> set over the identical <c>extrema_entity</c> fixture.
/// </summary>
public sealed class MariaDbSelectGroupLiveTests : IDisposable
{
    private static readonly object Gate = new();
    private static bool _seeded;
    private readonly MariaDbDataContext _ctx;

    public MariaDbSelectGroupLiveTests()
    {
        MariaDbContainer.IsAvailable.Should().BeTrue(
            "MariaDB must be reachable: " + (MariaDbContainer.Failure ?? "unavailable"));
        _ctx = new MariaDbDataContext(MariaDbContainer.ConnectionString, new DataContextBuilder());
        EnsureSeeded();
    }

    private static void EnsureSeeded()
    {
        if (_seeded) return;
        lock (Gate)
        {
            if (_seeded) return;
            using var connection = new MySqlConnection(MariaDbContainer.ConnectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
drop table if exists extrema_entity;
create table extrema_entity (id int not null primary key, score int null, category varchar(50) null, label varchar(50) not null);
insert into extrema_entity (id, score, category, label) values
  (1, null, 'a', 'one'), (2, 5, 'a', 'two'), (3, 9, 'a', 'three'), (4, 9, 'a', 'four'),
  (5, 3, 'b', 'five'), (6, 1, 'b', 'six'), (7, null, null, 'seven'), (8, 7, null, 'eight'),
  (9, 4, 'c', 'nine'), (10, 1, 'b', 'ten'), (11, null, 'd', 'eleven');";
            cmd.ExecuteNonQuery();
            _seeded = true;
        }
    }

    private EntityBuilder<ExtremaEntity> E => _ctx.From<ExtremaEntity>();

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void KeyedGrouping_ExactRows() => SelectGroupLiveOracles.KeyedGrouping(E);

    [Fact]
    public void SelectGroupHaving_Keyed_ExactRows() => SelectGroupLiveOracles.SelectGroupHaving(E);

    [Fact]
    public void SelectGroupHaving_GlobalAggregation_ExactTotalAndHavingOutcome() => SelectGroupLiveOracles.SelectGroupHavingGlobalAggregation(E);

    [Fact]
    public void SelectGroupHaving_ConstantProjection_OneRowAndEmptyInput() => SelectGroupLiveOracles.SelectGroupHavingConstantProjection(E);

    [Fact]
    public void HavingCombined_ExactSurvivingGroups() => SelectGroupLiveOracles.HavingCombined(E);

    [Fact]
    public void GlobalAggregation_ExactTotalAndHavingOutcome() => SelectGroupLiveOracles.GlobalAggregation(E);

    [Fact]
    public void ConstantProjection_OneRowAndEmptyInput() => SelectGroupLiveOracles.ConstantProjection(E);

    [Fact]
    public void NullableKey_NullGroupOnce() => SelectGroupLiveOracles.NullableKey(E);

    [Fact]
    public void OrderingTake_ExactPage() => SelectGroupLiveOracles.OrderingTake(E);
}
