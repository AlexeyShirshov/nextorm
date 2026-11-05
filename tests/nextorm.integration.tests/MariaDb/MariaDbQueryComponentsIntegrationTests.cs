using FluentAssertions;
using MySqlConnector;
using NextORM.Core;
using NextORM.MariaDb;
using NextORM.QueryComponents;

namespace NextORM.Integration.Tests;

[CollectionDefinition("MariaDb query components", DisableParallelization = true)]
public sealed class MariaDbQueryComponentsCollection;

[Collection("MariaDb query components")]
public sealed class MariaDbQueryComponentsIntegrationTests : IDisposable
{
    private static readonly object Gate = new();
    private static bool _seeded;
    private readonly MariaDbDataContext _ctx;

    public MariaDbQueryComponentsIntegrationTests()
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
    public void QueryComponents_MariaDb_MinOne_MaxAll_MatchBuiltIn()
    {
        var builtMin = E.SelectWhereMinOld(e => e.Score).ToList();
        var extMin = E.ExternalSelectWhereMin(e => e.Score).ToList();
        extMin.Select(e => e.Score).Should().Equal(builtMin.Select(e => e.Score));

        var builtMax = E.SelectWhereMaxOld(e => e.Score, ExtremeRowTies.All).ToList();
        var extMax = E.ExternalSelectWhereMax(e => e.Score, ExtremeRowTies.All).ToList();
        extMax.Select(e => e.Id).OrderBy(x => x).Should().Equal(builtMax.Select(e => e.Id).OrderBy(x => x));
    }

    [Fact]
    public void QueryComponents_MariaDb_CompositeAndGrouped_MatchBuiltIn()
    {
        var built = E.SelectWhereMaxOld(e => new { e.Category, e.Score }, ExtremeRowTies.All, e => new { e.Category, e.Label }).ToList();
        var ext = E.ExternalSelectWhereMax(e => new { e.Category, e.Score }, ExtremeRowTies.All, e => new { e.Category, e.Label }).ToList();
        ext.Select(e => e.Id).OrderBy(x => x).Should().Equal(built.Select(e => e.Id).OrderBy(x => x));
    }

    [Fact]
    public void QueryComponents_MariaDb_Projection_MatchBuiltIn()
    {
        var built = E.SelectWhereMinOld(e => e.Score, e => new { e.Id, e.Label }, ExtremeRowTies.All).ToList();
        var ext = E.ExternalSelectWhereMin(e => e.Score, e => new { e.Id, e.Label }, ExtremeRowTies.All).ToList();
        ext.Select(x => (x.Id, x.Label)).OrderBy(x => x.Id).Should().Equal(built.Select(x => (x.Id, x.Label)).OrderBy(x => x.Id));
    }

    [Fact]
    public void QueryComponents_MariaDb_WarmCache_Rebinds()
    {
        int threshold = 3;
        var cmd = (QueryCommand<ExtremaEntity>)E
            .Where(e => e.Score > threshold)
            .ExternalSelectWhereMin(e => e.Score);
        var first = _ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
        _ctx.ToList(first).Should().ContainSingle(e => e.Score == 4);
        threshold = 8;
        var second = _ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
        _ctx.ToList(second).Should().ContainSingle(e => e.Score == 9);
    }
}
