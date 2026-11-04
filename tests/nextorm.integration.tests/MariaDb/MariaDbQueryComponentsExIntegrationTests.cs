using FluentAssertions;
using MySqlConnector;
using NextORM.Core;
using NextORM.Extensions;
using NextORM.MariaDb;

namespace NextORM.Integration.Tests;

[CollectionDefinition("MariaDb query components ex", DisableParallelization = true)]
public sealed class MariaDbQueryComponentsExCollection;

[Collection("MariaDb query components ex")]
public sealed class MariaDbQueryComponentsExIntegrationTests : IDisposable
{
    private static readonly object Gate = new();
    private static bool _seeded;
    private readonly MariaDbDataContext _ctx;

    public MariaDbQueryComponentsExIntegrationTests()
    {
        Assert.SkipUnless(MariaDbContainer.IsAvailable, MariaDbContainer.Failure ?? "MariaDB is not available.");
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
    public void QueryComponentsEx_MariaDb_MinOne_MaxAll_MatchBuiltIn()
    {
        var builtMin = E.SelectWhereMin(e => e.Score).ToList();
        var exMin = E.SelectWhereMinEx(e => e.Score).ToList();
        // Global minimum (score 1) is tied between ids 6 and 10; tolerate the tie.
        builtMin.Should().ContainSingle();
        exMin.Should().ContainSingle();
        builtMin[0].Score.Should().Be(1);
        exMin[0].Score.Should().Be(1);
        builtMin[0].Id.Should().BeOneOf(6, 10);
        exMin[0].Id.Should().BeOneOf(6, 10);

        var builtMax = E.SelectWhereMax(e => e.Score, ExtremeRowTies.All).ToList();
        var exMax = E.SelectWhereMaxEx(e => e.Score, ExtremeRowTies.All).ToList();
        exMax.Select(e => e.Id).OrderBy(x => x).Should().Equal(builtMax.Select(e => e.Id).OrderBy(x => x));
    }

    [Fact]
    public void QueryComponentsEx_MariaDb_CompositeAndGrouped_MatchBuiltIn()
    {
        var built = E.SelectWhereMax(e => new { e.Category, e.Score }, ExtremeRowTies.All, e => new { e.Category, e.Label }).ToList();
        var ex = E.SelectWhereMaxEx(e => new { e.Category, e.Score }, ExtremeRowTies.All, e => new { e.Category, e.Label }).ToList();
        ex.Select(e => e.Id).OrderBy(x => x).Should().Equal(built.Select(e => e.Id).OrderBy(x => x));
    }

    [Fact]
    public void QueryComponentsEx_MariaDb_Projection_MatchBuiltIn()
    {
        var built = E.SelectWhereMin(e => e.Score, e => new { e.Id, e.Label }, ExtremeRowTies.All).ToList();
        var ex = E.SelectWhereMinEx(e => e.Score, e => new { e.Id, e.Label }, ExtremeRowTies.All).ToList();
        ex.Select(x => (x.Id, x.Label)).OrderBy(x => x.Id).Should().Equal(built.Select(x => (x.Id, x.Label)).OrderBy(x => x.Id));
    }

    [Fact]
    public void QueryComponentsEx_MariaDb_NullGroup_Parity()
    {
        var built = E.SelectWhereMax(e => e.Score, ExtremeRowTies.All, e => e.Category).ToList();
        var ex = E.SelectWhereMaxEx(e => e.Score, ExtremeRowTies.All, e => e.Category).ToList();
        ex.Select(e => e.Id).OrderBy(x => x).Should().Equal(built.Select(e => e.Id).OrderBy(x => x));
        ex.Should().Contain(e => e.Category == null);
    }
}
