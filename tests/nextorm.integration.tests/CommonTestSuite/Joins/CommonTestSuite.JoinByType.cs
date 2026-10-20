using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T222 (#222): runtime coverage for the source-less (type-only) join overloads. Inner/left/right
/// joins are compared row-for-row against their explicit-source oracle on every provider; full join
/// is compared where the provider supports it and otherwise both forms must be rejected identically
/// (never skipped — an unsupported capability is an equivalent rejection, see V28/V29).
/// </summary>
public abstract partial class CommonTestSuite
{
    [Fact]
    public void JoinByType_InnerJoin_EquivalentToExplicit()
    {
        var explicitRows = _sut.SimpleEntity
            .Join(_sut.ComplexEntity, (s, c) => s.Id == c.Id)
            .Select(p => new { p.Item1.Id, p.Item2.RequiredString })
            .ToList();
        var typeOnlyRows = _sut.SimpleEntity
            .Join<IComplexEntity>((s, c) => s.Id == c.Id)
            .Select(p => new { p.Item1.Id, p.Item2.RequiredString })
            .ToList();

        typeOnlyRows.Should().BeEquivalentTo(explicitRows);
        explicitRows.Should().NotBeEmpty();
    }

    [Fact]
    public void JoinByType_LeftJoin_EquivalentToExplicit()
    {
        var explicitRows = _sut.SimpleEntity
            .LeftJoin(_sut.ComplexEntity, (s, c) => s.Id == c.Id)
            .Select(p => new { p.Item1.Id, Right = p.Item2.RequiredString })
            .ToList();
        var typeOnlyRows = _sut.SimpleEntity
            .LeftJoin<IComplexEntity>((s, c) => s.Id == c.Id)
            .Select(p => new { p.Item1.Id, Right = p.Item2.RequiredString })
            .ToList();

        typeOnlyRows.Should().BeEquivalentTo(explicitRows);
        typeOnlyRows.Count(r => r.Right is null).Should().BeGreaterThan(0);
    }

    [Fact]
    public void JoinByType_RightJoin_EquivalentToExplicit()
    {
        var explicitRows = _sut.ComplexEntity
            .RightJoin(_sut.SimpleEntity, (c, s) => c.Id == s.Id)
            .Select(p => new { Left = p.Item1.RequiredString, p.Item2.Id })
            .ToList();
        var typeOnlyRows = _sut.ComplexEntity
            .RightJoin<ISimpleEntity>((c, s) => c.Id == s.Id)
            .Select(p => new { Left = p.Item1.RequiredString, p.Item2.Id })
            .ToList();

        typeOnlyRows.Should().BeEquivalentTo(explicitRows);
        typeOnlyRows.Count(r => r.Left is null).Should().BeGreaterThan(0);
    }

    [Fact]
    public void JoinByType_ChainedArity_EquivalentToExplicit()
    {
        var explicitRows = _sut.SimpleEntity
            .Join(_sut.ComplexEntity, (s, c) => s.Id == c.Id)
            .Join(_sut.SimpleEntity, (p, s2) => p.Item2.Id == s2.Id)
            .Select(p => new { LeftId = p.Item1.Id, ThirdId = p.Item3.Id })
            .ToList();
        var typeOnlyRows = _sut.SimpleEntity
            .Join<IComplexEntity>((s, c) => s.Id == c.Id)
            .Join<ISimpleEntity>((p, s2) => p.Item2.Id == s2.Id)
            .Select(p => new { LeftId = p.Item1.Id, ThirdId = p.Item3.Id })
            .ToList();

        typeOnlyRows.Should().BeEquivalentTo(explicitRows);
        explicitRows.Should().NotBeEmpty();
    }

    [Fact]
    public void JoinByType_FullJoin_EquivalentToExplicitOrEquivalentlyRejected()
    {
        if (!Provider.SupportsFullJoin)
        {
            var explicitAct = () => _sut.SimpleEntity
                .FullJoin(_sut.ComplexEntity, (s, c) => s.Id == c.Id)
                .Select(p => p.Item1.Id)
                .ToList();
            var typeOnlyAct = () => _sut.SimpleEntity
                .FullJoin<IComplexEntity>((s, c) => s.Id == c.Id)
                .Select(p => p.Item1.Id)
                .ToList();

            explicitAct.Should().Throw<NotSupportedException>();
            typeOnlyAct.Should().Throw<NotSupportedException>();
            return;
        }

        var explicitRows = _sut.SimpleEntity
            .FullJoin(_sut.ComplexEntity, (s, c) => s.Id == c.Id)
            .Select(p => new { p.Item1.Id, Right = p.Item2.RequiredString })
            .ToList();
        var typeOnlyRows = _sut.SimpleEntity
            .FullJoin<IComplexEntity>((s, c) => s.Id == c.Id)
            .Select(p => new { p.Item1.Id, Right = p.Item2.RequiredString })
            .ToList();

        typeOnlyRows.Should().BeEquivalentTo(explicitRows);
        explicitRows.Should().NotBeEmpty();
    }
}

/// <summary>
/// ClickHouse does not derive <see cref="CommonTestSuite"/> (it owns its own container harness), so
/// the shared type-only join facts are re-pinned here against a real ClickHouse server.
/// </summary>
public sealed class ClickHouseJoinByTypeIntegrationTests : IDisposable
{
    private readonly IDataContext _ctx;

    public ClickHouseJoinByTypeIntegrationTests()
    {
        Assert.SkipUnless(ClickHouseContainer.IsAvailable, ClickHouseContainer.Failure ?? "ClickHouse is not available.");

        _ctx = new ClickHouseDataContext(ClickHouseContainer.ConnectionString, new DataContextBuilder());
        Seed();
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void JoinByType_InnerJoin_EquivalentToExplicit()
    {
        var explicitRows = _ctx.From<ChJbParent>()
            .Join(_ctx.From<ChJbChild>(), (p, c) => p.Id == c.ParentId)
            .Select(p => new { p.Item1.Id, p.Item2.Value })
            .ToList();
        var typeOnlyRows = _ctx.From<ChJbParent>()
            .Join<ChJbChild>((p, c) => p.Id == c.ParentId)
            .Select(p => new { p.Item1.Id, p.Item2.Value })
            .ToList();

        typeOnlyRows.Should().BeEquivalentTo(explicitRows);
        explicitRows.Should().NotBeEmpty();
    }

    [Fact]
    public void JoinByType_LeftJoin_EquivalentToExplicit()
    {
        var explicitRows = _ctx.From<ChJbParent>()
            .LeftJoin(_ctx.From<ChJbChild>(), (p, c) => p.Id == c.ParentId)
            .Select(p => new { p.Item1.Id, Right = p.Item2.Value })
            .ToList();
        var typeOnlyRows = _ctx.From<ChJbParent>()
            .LeftJoin<ChJbChild>((p, c) => p.Id == c.ParentId)
            .Select(p => new { p.Item1.Id, Right = p.Item2.Value })
            .ToList();

        typeOnlyRows.Should().BeEquivalentTo(explicitRows);
    }

    [Fact]
    public void JoinByType_ChainedArity_EquivalentToExplicit()
    {
        var explicitRows = _ctx.From<ChJbParent>()
            .Join(_ctx.From<ChJbChild>(), (p, c) => p.Id == c.ParentId)
            .Join(_ctx.From<ChJbChild>(), (p, c) => p.Item2.Id == c.Id)
            .Select(p => new { p.Item1.Id, p.Item3.Value })
            .ToList();
        var typeOnlyRows = _ctx.From<ChJbParent>()
            .Join<ChJbChild>((p, c) => p.Id == c.ParentId)
            .Join<ChJbChild>((p, c) => p.Item2.Id == c.Id)
            .Select(p => new { p.Item1.Id, p.Item3.Value })
            .ToList();

        typeOnlyRows.Should().BeEquivalentTo(explicitRows);
    }

    private void Seed()
    {
        Execute("drop table if exists tjb_child");
        Execute("drop table if exists tjb_parent");
        Execute("create table tjb_parent (id Int32, parent_label Nullable(String)) engine = Memory");
        Execute("create table tjb_child (id Int32, parent_id Int32, child_value Nullable(String)) engine = Memory");
        Execute("insert into tjb_parent (id, parent_label) values (1, 'p1'), (2, 'p2')");
        Execute("insert into tjb_child (id, parent_id, child_value) values (10, 1, 'c1'), (11, 1, 'c2')");
    }

    private void Execute(string sql)
    {
        ((DataContext)_ctx).EnsureConnectionOpen();
        using var cmd = ((DataContext)_ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }
}

[SqlTable("tjb_parent")]
internal sealed class ChJbParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
}

[SqlTable("tjb_child")]
internal sealed class ChJbChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("child_value")]
    public string? Value { get; set; }
}
