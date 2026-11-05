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

    private static int[] Ids(IEnumerable<ExtremaEntity> rows) => rows.Select(e => e.Id).OrderBy(id => id).ToArray();

    [Fact]
    public void MinOne_And_MaxAll_Scalar_Global()
    {
        var min = E.SelectWhereMin(e => e.Score).ToList();
        min.Should().ContainSingle();
        min[0].Score.Should().Be(1);
        min[0].Id.Should().BeOneOf(6, 10);

        var max = E.SelectWhereMax(e => e.Score, ExtremeRowTies.All).ToList();
        Ids(max).Should().Equal(3, 4);
    }

    [Fact]
    public void CompositeKey_Grouped_ComparesLexicographicallyPerGroup()
    {
        // The group key is (Category, Label), so every row is its own group; the value key is
        // (Category, Score), so any row with a NULL in either key is excluded (1, 7, 8, 11). `All`
        // therefore keeps exactly the remaining rows.
        var rows = E.SelectWhereMax(
            e => new { e.Category, e.Score },
            ExtremeRowTies.All,
            e => new { e.Category, e.Label }).ToList();

        Ids(rows).Should().Equal(2, 3, 4, 5, 6, 9, 10);
    }

    [Fact]
    public void Projection_All_KeepsTheTiedWinners()
    {
        var rows = E.SelectWhereMin(e => e.Score, e => new { e.Id, e.Label }, ExtremeRowTies.All).ToList();

        rows.Select(x => (x.Id, x.Label)).OrderBy(x => x.Id).Should().Equal((6, "six"), (10, "ten"));
    }

    [Fact]
    public void GroupedAll_KeepsTheNullGroup()
    {
        var rows = E.SelectWhereMax(e => e.Score, ExtremeRowTies.All, e => e.Category).ToList();

        Ids(rows).Should().Equal(3, 4, 5, 8, 9);
        rows.Should().Contain(e => e.Category == null);
    }
}
