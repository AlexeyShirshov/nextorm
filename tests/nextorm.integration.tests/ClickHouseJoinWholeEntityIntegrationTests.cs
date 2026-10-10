using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// ClickHouse-only runtime coverage for a direct whole-entity projection of a joined item (#190).
/// ClickHouse does not derive <see cref="CommonTestSuite"/> (it has its own
/// <see cref="ClickHouseContainer"/> harness), so the shared direct-entity facts are re-pinned here
/// against a real ClickHouse server.
/// </summary>
public sealed class ClickHouseJoinWholeEntityIntegrationTests : IDisposable
{
    private readonly IDataContext _ctx;

    public ClickHouseJoinWholeEntityIntegrationTests()
    {
        Assert.SkipUnless(ClickHouseContainer.IsAvailable, ClickHouseContainer.Failure ?? "ClickHouse is not available.");

        _ctx = new ClickHouseDataContext(ClickHouseContainer.ConnectionString, new DataContextBuilder());
        Seed();
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void JoinWholeEntity_DirectChild_InnerJoin_ShouldMaterializeEntity()
    {
        var rows = _ctx.From<ChJwParent>()
            .Join(_ctx.From<ChJwChild>(), (p, c) => p.Id == c.ParentId)
            .Select(p => p.Item2)
            .ToList();

        rows.Should().HaveCount(2);
        rows.Select(c => c.Id).OrderBy(x => x).Should().Equal(10, 11);
        rows.Select(c => c.Value).OrderBy(x => x).Should().Equal("c1", "c2");
    }

    [Fact]
    public void JoinWholeEntity_DirectChild_OuterJoin_ShouldReturnNullForMissingSide()
    {
        // ClickHouse fills the unmatched side of an explicit outer join with defaults unless
        // `join_use_nulls = 1` is set query-locally (the dialect injects it only for implicit
        // reference navigations), so request it explicitly to observe the SQL NULL contract.
        var rows = _ctx.From<ChJwParent>()
            .Settings(("join_use_nulls", "1"))
            .LeftJoin(_ctx.From<ChJwChild>(), (p, c) => p.Id == c.ParentId)
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
        Execute("create table jw_parent (id Int32, parent_label Nullable(String)) engine = Memory");
        Execute("create table jw_child (id Int32, parent_id Int32, child_value Nullable(String)) engine = Memory");
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
internal sealed class ChJwParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_label")]
    public string? Label { get; set; }
}

[SqlTable("jw_child")]
internal sealed class ChJwChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("child_value")]
    public string? Value { get; set; }
}
