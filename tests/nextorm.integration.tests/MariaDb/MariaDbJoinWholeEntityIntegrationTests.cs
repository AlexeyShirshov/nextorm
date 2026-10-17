using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;
using NextORM.MariaDb;

namespace NextORM.Integration.Tests;

/// <summary>
/// MariaDB-only runtime coverage for a direct whole-entity projection of a joined item (#190).
/// MariaDB has no <see cref="CommonTestSuite"/>-based integration class (it uses its own
/// <see cref="MariaDbContainer"/> harness), so the shared direct-entity facts are re-pinned here
/// against a real MariaDB server.
/// </summary>
public sealed class MariaDbJoinWholeEntityIntegrationTests : IDisposable
{
    private readonly IDataContext _ctx;

    public MariaDbJoinWholeEntityIntegrationTests()
    {
        Assert.SkipUnless(MariaDbContainer.IsAvailable, MariaDbContainer.Failure ?? "MariaDB is not available.");

        _ctx = new MariaDbDataContext(MariaDbContainer.ConnectionString, new DataContextBuilder());
        Seed();
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void JoinWholeEntity_DirectChild_InnerJoin_ShouldMaterializeEntity()
    {
        var rows = _ctx.From<JwParent>()
            .Join(_ctx.From<JwChild>(), (p, c) => p.Id == c.ParentId)
            .Select(p => p.Item2)
            .ToList();

        rows.Should().HaveCount(2);
        rows.Select(c => c.Id).OrderBy(x => x).Should().Equal(10, 11);
        rows.Select(c => c.Value).OrderBy(x => x).Should().Equal("c1", "c2");
    }

    [Fact]
    public void JoinWholeEntity_DirectChild_OuterJoin_ShouldReturnNullForMissingSide()
    {
        var rows = _ctx.From<JwParent>()
            .LeftJoin(_ctx.From<JwChild>(), (p, c) => p.Id == c.ParentId)
            .Select(p => p.Item2)
            .ToList();

        rows.Should().HaveCount(3);
        rows.Count(c => c is null).Should().Be(1, "the missing outer-join side must materialize as null");
        rows.Where(c => c is not null).Select(c => c!.Id).OrderBy(x => x).Should().Equal(10, 11);
    }

    private void Seed()
    {
        Execute("drop table if exists jw_child");
        Execute("drop table if exists jw_parent");
        Execute("create table jw_parent (id int not null primary key, parent_label varchar(50) null)");
        Execute("create table jw_child (id int not null primary key, parent_id int not null, child_value varchar(50) null)");
        Execute("insert into jw_parent (id, parent_label) values (1, 'p1'), (2, 'p2')");
        Execute("insert into jw_child (id, parent_id, child_value) values (10, 1, 'c1'), (11, 1, 'c2')");
    }

    private void Execute(string sql)
    {
        ((DataContext)_ctx).EnsureConnectionOpen();
        using var cmd = ((DataContext)_ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }
}

[SqlTable("jw_parent")]
internal sealed class JwParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_label")]
    public string? Label { get; set; }
}

[SqlTable("jw_child")]
internal sealed class JwChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("child_value")]
    public string? Value { get; set; }
}
