using System.Linq.Expressions;
using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Postgres;
using NextORM.SqlServer;

namespace NextORM.Core.Tests;

public class ArrayTestEntity
{
    public int Id { get; set; }
    public string[] Tags { get; set; } = [];
}

public class InMemoryJoinTests
{
    private readonly InMemoryRepository _sut;
    public InMemoryJoinTests(InMemoryRepository sut)
    {
        _sut = sut;
        _sut.SimpleEntity.WithData(new[] { new SimpleEntity { Id = 1 }, new SimpleEntity { Id = 2 } });
    }
    [Fact]
    public async Task TestJoin()
    {
        var query = _sut.SimpleEntity.Join(_sut.SimpleEntity, (t1, t2) => t1.Id == t2.Id + 1).Select(p => new { FirstId = p.Item1.Id, SecondId = p.Item2.Id });

        var idx = 0;
        await foreach (var row in query.ToAsyncEnumerable())
        {
            idx++;
            row.FirstId.Should().Be(row.SecondId + 1);
        }

        idx.Should().BeGreaterThan(0);
    }
    [Fact]
    public async Task TestJoin2()
    {
        var query = _sut.SimpleEntity
            .Join(_sut.SimpleEntity, (t1, t2) => t1.Id == t2.Id + 1)
            .Join(_sut.SimpleEntity, (p, t3) => p.Item2.Id == t3.Id - 1)
            .Select(p => new { FirstId = p.Item1.Id, SecondId = p.Item2.Id, ThirdId = p.Item3.Id });

        var idx = 0;
        await foreach (var row in query.ToAsyncEnumerable())
        {
            idx++;
            row.FirstId.Should().Be(row.SecondId + 1);
            row.FirstId.Should().Be(row.ThirdId);
        }

        idx.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task TestCrossJoin()
    {
        var query = _sut.SimpleEntity
            .CrossJoin(_sut.SimpleEntity)
            .Select(p => new { FirstId = p.Item1.Id, SecondId = p.Item2.Id });

        var count = 0;
        await foreach (var row in query.ToAsyncEnumerable())
        {
            count++;
            row.FirstId.Should().BeGreaterThan(0);
            row.SecondId.Should().BeGreaterThan(0);
        }

        count.Should().Be(4);
    }

    [Fact]
    public void TestJoinStrictness_ShouldThrow()
    {
        var act = () => _sut.SimpleEntity
            .Join(_sut.SimpleEntity, (t1, t2) => t1.Id == t2.Id, j => j.WithStrictness(JoinStrictness.Any))
            .Select(p => new { FirstId = p.Item1.Id })
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*join modifier is not supported*");
    }

    [Fact]
    public void TestGlobalJoin_ShouldThrow()
    {
        var act = () => _sut.SimpleEntity
            .Join(_sut.SimpleEntity, (t1, t2) => t1.Id == t2.Id, j => j.Global())
            .Select(p => new { FirstId = p.Item1.Id })
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*GLOBAL join modifier is not supported*");
    }

    [Fact]
    public void TestJoinHint_ShouldThrow()
    {
        var act = () => _sut.SimpleEntity
            .Join(_sut.SimpleEntity, (t1, t2) => t1.Id == t2.Id, j => j.WithJoinHint("loop"))
            .Select(p => new { FirstId = p.Item1.Id })
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*hints are not supported*");
    }

    [Fact]
    public void TestJoinTableHint_ShouldThrow()
    {
        var act = () => _sut.SimpleEntity
            .Join(_sut.SimpleEntity, (t1, t2) => t1.Id == t2.Id, j => j.WithJoinTableHint("nolock"))
            .Select(p => new { FirstId = p.Item1.Id })
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*hints are not supported*");
    }

    [Fact]
    public void TestTableHint_ShouldThrow()
    {
        var act = () => _sut.DataProvider.From<SimpleEntity>(o => o.WithTableHint("nolock"))
            .Select(p => new { p.Id })
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*hints are not supported*");
    }

    [Fact]
    public void TestTablesInScopeHint_ShouldThrow()
    {
        var act = () => _sut.SimpleEntity
            .WithTablesInScopeHint("nolock")
            .Select(p => new { p.Id })
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*hints are not supported*");
    }

    [Fact]
    public void TestSemiAntiPasteJoin_ShouldThrow()
    {
        var semi = () => _sut.SimpleEntity
            .SemiJoin(_sut.SimpleEntity, (t1, t2) => t1.Id == t2.Id)
            .Select(t1 => new { t1.Id })
            .ToList();
        semi.Should().Throw<NotSupportedException>().WithMessage("*Semi join is not supported*");

        var anti = () => _sut.SimpleEntity
            .AntiJoin(_sut.SimpleEntity, (t1, t2) => t1.Id == t2.Id)
            .Select(t1 => new { t1.Id })
            .ToList();
        anti.Should().Throw<NotSupportedException>().WithMessage("*Anti join is not supported*");

        var paste = () => _sut.SimpleEntity
            .PasteJoin(_sut.SimpleEntity)
            .Select(p => new { p.Item1.Id, Second = p.Item2.Id })
            .ToList();
        paste.Should().Throw<NotSupportedException>().WithMessage("*Paste join is not supported*");
    }

    [Fact]
    public async Task TestCrossJoin8Tables_ShouldCloneAndMaterializeAtEveryArity()
    {
        // A single row per table keeps the cross product at one row while still walking every
        // Projection<T1..Tn> (its Extend is what accumulates the join) and every EntityPn clone.
        _sut.SimpleEntity.WithData([new SimpleEntity { Id = 1 }]);

        var p2 = _sut.SimpleEntity.CrossJoin(_sut.SimpleEntity);
        var p2c = p2.Clone();
        p2c.Joins.Should().HaveCount(1);

        var p3 = p2c.CrossJoin(_sut.SimpleEntity);
        var p3c = p3.Clone();
        p3c.Joins.Should().HaveCount(2);

        var p4 = p3c.CrossJoin(_sut.SimpleEntity);
        var p4c = p4.Clone();
        p4c.Joins.Should().HaveCount(3);

        var p5 = p4c.CrossJoin(_sut.SimpleEntity);
        var p5c = p5.Clone();
        p5c.Joins.Should().HaveCount(4);

        var p6 = p5c.CrossJoin(_sut.SimpleEntity);
        var p6c = p6.Clone();
        p6c.Joins.Should().HaveCount(5);

        var p7 = p6c.CrossJoin(_sut.SimpleEntity);
        var p7c = p7.Clone();
        p7c.Joins.Should().HaveCount(6);

        var p8 = p7c.CrossJoin(_sut.SimpleEntity);
        var p8c = p8.Clone();
        p8c.Joins.Should().HaveCount(7);

        var count = 0;
        await foreach (var row in p8c.Select(p => p.Item1.Id).ToAsyncEnumerable())
            count++;

        count.Should().Be(1);
    }

    [Fact]
    public async Task TestLeftJoin()
    {
        var query = _sut.SimpleEntity
            .LeftJoin(_sut.SimpleEntity, (t1, t2) => t1.Id == t2.Id + 1)
            .Select(p => new { FirstId = p.Item1.Id, Right = p.Item2 });

        var rows = new List<(int FirstId, SimpleEntity? Right)>();
        await foreach (var row in query.ToAsyncEnumerable())
        {
            rows.Add((row.FirstId, row.Right));
        }

        rows.Should().HaveCount(2);
        rows.Count(r => r.Right is null).Should().Be(1);
    }

    [Fact]
    public async Task TestJoin4()
    {
        var query = _sut.SimpleEntity
            .Join(_sut.SimpleEntity, (t1, t2) => t1.Id == t2.Id + 1)
            .Join(_sut.SimpleEntity, (p, t3) => p.Item2.Id == t3.Id - 1)
            .Join(_sut.SimpleEntity, (p, t4) => p.Item3.Id == t4.Id)
            .Select(p => new { FirstId = p.Item1.Id, SecondId = p.Item2.Id, ThirdId = p.Item3.Id, FourthId = p.Item4.Id });

        var idx = 0;
        await foreach (var row in query.ToAsyncEnumerable())
        {
            idx++;
            row.FirstId.Should().Be(row.SecondId + 1);
            row.FirstId.Should().Be(row.ThirdId);
            row.ThirdId.Should().Be(row.FourthId);
        }

        idx.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task TestRightJoinChained()
    {
        var query = _sut.SimpleEntity
            .Join(_sut.SimpleEntity, (t1, t2) => t1.Id == t2.Id + 1)
            .RightJoin(_sut.SimpleEntity, (p, t3) => p.Item2.Id == t3.Id - 1)
            .Select(p => new { RightId = p.Item3.Id, Left = p.Item1, Mid = p.Item2 });

        var rows = new List<(int RightId, SimpleEntity? Left, SimpleEntity? Mid)>();
        await foreach (var row in query.ToAsyncEnumerable())
            rows.Add((row.RightId, row.Left, row.Mid));

        rows.Should().HaveCount(2);
        rows.Should().ContainSingle(r => r.Left == null && r.Mid == null && r.RightId == 1);
        rows.Should().ContainSingle(r => r.Left != null && r.Mid != null && r.Left.Id == 2 && r.Mid.Id == 1 && r.RightId == 2);
    }

    [Fact]
    public async Task TestFullJoinChained()
    {
        var query = _sut.SimpleEntity
            .Join(_sut.SimpleEntity, (t1, t2) => t1.Id == t2.Id + 1)
            .FullJoin(_sut.SimpleEntity, (p, t3) => p.Item2.Id == t3.Id - 1)
            .Select(p => new { RightId = p.Item3.Id, Left = p.Item1, Mid = p.Item2 });

        var rows = new List<(int RightId, SimpleEntity? Left, SimpleEntity? Mid)>();
        await foreach (var row in query.ToAsyncEnumerable())
            rows.Add((row.RightId, row.Left, row.Mid));

        rows.Should().HaveCount(2);
        rows.Should().ContainSingle(r => r.Left == null && r.Mid == null && r.RightId == 1);
        rows.Should().ContainSingle(r => r.Left != null && r.Mid != null && r.Left.Id == 2 && r.Mid.Id == 1 && r.RightId == 2);
    }

    [Fact]
    public void TestArrayJoinClause_ShouldThrow()
    {
        var e = _sut.DataProvider.From<ArrayTestEntity>();

        var act = () => e.ArrayJoin(x => x.Tags).Select(x => new { x.Id }).ToList();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void TestArrayJoinElement_ShouldThrow()
    {
        var e = _sut.DataProvider.From<ArrayTestEntity>();

        var act = () => e.ArrayJoinElement(x => x.Tags).Select(p => new { p.Item1.Id, Tag = p.Element }).ToList();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void TestDerivedSourceThenJoin_ShouldThrow()
    {
        var derived = _sut.SimpleEntity.Select(x => new { x.Id });

        var act = () => _sut.DataProvider.From(derived)
            .Join(_sut.SimpleEntity, (d, s) => d.Id == s.Id)
            .Select(p => new { p.Item1.Id, SId = p.Item2.Id })
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*derived query*primary FROM*");
    }

    [Fact]
    public void TestAsThenJoin_ShouldThrow()
    {
        var act = () => _sut.SimpleEntity
            .Join(_sut.SimpleEntity, (t1, t2) => t1.Id == t2.Id)
            .As(p => new { LeftId = p.Item1.Id, RightId = p.Item2.Id })
            .Join(_sut.SimpleEntity, (d, t3) => d.LeftId == t3.Id)
            .Select(p => new { p.Item1.LeftId, Third = p.Item2.Id })
            .ToList();

        // As wraps the projection in a derived source; joining it is what the in-memory provider rejects.
        act.Should().Throw<NotSupportedException>().WithMessage("*derived query*primary FROM*");
    }
    // ---------------------------------------------------------------------------------------------
    // T222: source-less (type-only) join overloads. Everything below pins the new overloads to the
    // existing explicit-source oracle: same result rows, same options forwarding, same fail-closed
    // guards. No new planner/cache/materialization behaviour is introduced.
    // ---------------------------------------------------------------------------------------------

    private static async Task<List<int>> IdsAsync<TEntity>(EntityBuilder<TEntity> builder, Expression<Func<TEntity, int>> selector)
    {
        var rows = new List<int>();
        await foreach (var id in builder.Select(selector).ToAsyncEnumerable())
            rows.Add(id);
        return rows;
    }

    private static async Task AssertSameIds<TEntity>(EntityBuilder<TEntity> explicitBuilder, EntityBuilder<TEntity> typeOnlyBuilder, Expression<Func<TEntity, int>> selector)
    {
        var expected = await IdsAsync(explicitBuilder, selector);
        var actual = await IdsAsync(typeOnlyBuilder, selector);
        actual.Should().Equal(expected);
    }

    [Fact]
    public async Task TestTypeOnlyInnerJoin_EquivalentToExplicit_AtEveryReceiverArity()
    {
        _sut.SimpleEntity.WithData([new SimpleEntity { Id = 1 }, new SimpleEntity { Id = 2 }]);

        var e2 = _sut.SimpleEntity.Join(_sut.SimpleEntity, (t1, t2) => t1.Id == t2.Id + 1);
        var t2 = _sut.SimpleEntity.Join<SimpleEntity>((t1, t2) => t1.Id == t2.Id + 1);
        await AssertSameIds(e2, t2, p => p.Item1.Id);

        var e3 = e2.Join(_sut.SimpleEntity, (p, t) => p.Item2 != null && p.Item2.Id == t.Id);
        var t3 = t2.Join<SimpleEntity>((p, t) => p.Item2 != null && p.Item2.Id == t.Id);
        await AssertSameIds(e3, t3, p => p.Item1.Id);

        var e4 = e3.Join(_sut.SimpleEntity, (p, t) => p.Item2 != null && p.Item2.Id == t.Id);
        var t4 = t3.Join<SimpleEntity>((p, t) => p.Item2 != null && p.Item2.Id == t.Id);
        await AssertSameIds(e4, t4, p => p.Item1.Id);

        var e5 = e4.Join(_sut.SimpleEntity, (p, t) => p.Item2 != null && p.Item2.Id == t.Id);
        var t5 = t4.Join<SimpleEntity>((p, t) => p.Item2 != null && p.Item2.Id == t.Id);
        await AssertSameIds(e5, t5, p => p.Item1.Id);

        var e6 = e5.Join(_sut.SimpleEntity, (p, t) => p.Item2 != null && p.Item2.Id == t.Id);
        var t6 = t5.Join<SimpleEntity>((p, t) => p.Item2 != null && p.Item2.Id == t.Id);
        await AssertSameIds(e6, t6, p => p.Item1.Id);

        var e7 = e6.Join(_sut.SimpleEntity, (p, t) => p.Item2 != null && p.Item2.Id == t.Id);
        var t7 = t6.Join<SimpleEntity>((p, t) => p.Item2 != null && p.Item2.Id == t.Id);
        await AssertSameIds(e7, t7, p => p.Item1.Id);

        var e8 = e7.Join(_sut.SimpleEntity, (p, t) => p.Item2 != null && p.Item2.Id == t.Id);
        var t8 = t7.Join<SimpleEntity>((p, t) => p.Item2 != null && p.Item2.Id == t.Id);
        await AssertSameIds(e8, t8, p => p.Item1.Id);
    }

    [Fact]
    public async Task TestTypeOnlyLeftJoin_EquivalentToExplicit_AtEveryReceiverArity()
    {
        _sut.SimpleEntity.WithData([new SimpleEntity { Id = 1 }, new SimpleEntity { Id = 2 }]);

        var e2 = _sut.SimpleEntity.LeftJoin(_sut.SimpleEntity, (t1, t2) => t1.Id == t2.Id + 1);
        var t2 = _sut.SimpleEntity.LeftJoin<SimpleEntity>((t1, t2) => t1.Id == t2.Id + 1);
        await AssertSameIds(e2, t2, p => p.Item1.Id);

        var e3 = e2.LeftJoin(_sut.SimpleEntity, (p, t) => p.Item2 != null && p.Item2.Id == t.Id);
        var t3 = t2.LeftJoin<SimpleEntity>((p, t) => p.Item2 != null && p.Item2.Id == t.Id);
        await AssertSameIds(e3, t3, p => p.Item1.Id);

        var e4 = e3.LeftJoin(_sut.SimpleEntity, (p, t) => p.Item2 != null && p.Item2.Id == t.Id);
        var t4 = t3.LeftJoin<SimpleEntity>((p, t) => p.Item2 != null && p.Item2.Id == t.Id);
        await AssertSameIds(e4, t4, p => p.Item1.Id);

        var e5 = e4.LeftJoin(_sut.SimpleEntity, (p, t) => p.Item2 != null && p.Item2.Id == t.Id);
        var t5 = t4.LeftJoin<SimpleEntity>((p, t) => p.Item2 != null && p.Item2.Id == t.Id);
        await AssertSameIds(e5, t5, p => p.Item1.Id);

        var e6 = e5.LeftJoin(_sut.SimpleEntity, (p, t) => p.Item2 != null && p.Item2.Id == t.Id);
        var t6 = t5.LeftJoin<SimpleEntity>((p, t) => p.Item2 != null && p.Item2.Id == t.Id);
        await AssertSameIds(e6, t6, p => p.Item1.Id);

        var e7 = e6.LeftJoin(_sut.SimpleEntity, (p, t) => p.Item2 != null && p.Item2.Id == t.Id);
        var t7 = t6.LeftJoin<SimpleEntity>((p, t) => p.Item2 != null && p.Item2.Id == t.Id);
        await AssertSameIds(e7, t7, p => p.Item1.Id);

        var e8 = e7.LeftJoin(_sut.SimpleEntity, (p, t) => p.Item2 != null && p.Item2.Id == t.Id);
        var t8 = t7.LeftJoin<SimpleEntity>((p, t) => p.Item2 != null && p.Item2.Id == t.Id);
        await AssertSameIds(e8, t8, p => p.Item1.Id);
    }

    [Fact]
    public async Task TestTypeOnlyRightJoin_EquivalentToExplicit_AtEveryReceiverArity()
    {
        _sut.SimpleEntity.WithData([new SimpleEntity { Id = 1 }, new SimpleEntity { Id = 2 }]);

        var e2 = _sut.SimpleEntity.RightJoin(_sut.SimpleEntity, (t1, t2) => t1.Id == t2.Id);
        var t2 = _sut.SimpleEntity.RightJoin<SimpleEntity>((t1, t2) => t1.Id == t2.Id);
        await AssertSameIds(e2, t2, p => p.Item1.Id);

        var e3 = e2.RightJoin(_sut.SimpleEntity, (p, t) => p.Item2.Id == t.Id);
        var t3 = t2.RightJoin<SimpleEntity>((p, t) => p.Item2.Id == t.Id);
        await AssertSameIds(e3, t3, p => p.Item1.Id);

        var e4 = e3.RightJoin(_sut.SimpleEntity, (p, t) => p.Item2.Id == t.Id);
        var t4 = t3.RightJoin<SimpleEntity>((p, t) => p.Item2.Id == t.Id);
        await AssertSameIds(e4, t4, p => p.Item1.Id);

        var e5 = e4.RightJoin(_sut.SimpleEntity, (p, t) => p.Item2.Id == t.Id);
        var t5 = t4.RightJoin<SimpleEntity>((p, t) => p.Item2.Id == t.Id);
        await AssertSameIds(e5, t5, p => p.Item1.Id);

        var e6 = e5.RightJoin(_sut.SimpleEntity, (p, t) => p.Item2.Id == t.Id);
        var t6 = t5.RightJoin<SimpleEntity>((p, t) => p.Item2.Id == t.Id);
        await AssertSameIds(e6, t6, p => p.Item1.Id);

        var e7 = e6.RightJoin(_sut.SimpleEntity, (p, t) => p.Item2.Id == t.Id);
        var t7 = t6.RightJoin<SimpleEntity>((p, t) => p.Item2.Id == t.Id);
        await AssertSameIds(e7, t7, p => p.Item1.Id);

        var e8 = e7.RightJoin(_sut.SimpleEntity, (p, t) => p.Item2.Id == t.Id);
        var t8 = t7.RightJoin<SimpleEntity>((p, t) => p.Item2.Id == t.Id);
        await AssertSameIds(e8, t8, p => p.Item1.Id);

    }

    [Fact]
    public async Task TestTypeOnlyFullJoin_EquivalentToExplicit_AtEveryReceiverArity()
    {
        _sut.SimpleEntity.WithData([new SimpleEntity { Id = 1 }, new SimpleEntity { Id = 2 }]);

        var e2 = _sut.SimpleEntity.FullJoin(_sut.SimpleEntity, (t1, t2) => t1.Id == t2.Id);
        var t2 = _sut.SimpleEntity.FullJoin<SimpleEntity>((t1, t2) => t1.Id == t2.Id);
        await AssertSameIds(e2, t2, p => p.Item1.Id);

        var e3 = e2.FullJoin(_sut.SimpleEntity, (p, t) => p.Item2.Id == t.Id);
        var t3 = t2.FullJoin<SimpleEntity>((p, t) => p.Item2.Id == t.Id);
        await AssertSameIds(e3, t3, p => p.Item1.Id);

        var e4 = e3.FullJoin(_sut.SimpleEntity, (p, t) => p.Item2.Id == t.Id);
        var t4 = t3.FullJoin<SimpleEntity>((p, t) => p.Item2.Id == t.Id);
        await AssertSameIds(e4, t4, p => p.Item1.Id);

        var e5 = e4.FullJoin(_sut.SimpleEntity, (p, t) => p.Item2.Id == t.Id);
        var t5 = t4.FullJoin<SimpleEntity>((p, t) => p.Item2.Id == t.Id);
        await AssertSameIds(e5, t5, p => p.Item1.Id);

        var e6 = e5.FullJoin(_sut.SimpleEntity, (p, t) => p.Item2.Id == t.Id);
        var t6 = t5.FullJoin<SimpleEntity>((p, t) => p.Item2.Id == t.Id);
        await AssertSameIds(e6, t6, p => p.Item1.Id);

        var e7 = e6.FullJoin(_sut.SimpleEntity, (p, t) => p.Item2.Id == t.Id);
        var t7 = t6.FullJoin<SimpleEntity>((p, t) => p.Item2.Id == t.Id);
        await AssertSameIds(e7, t7, p => p.Item1.Id);

        var e8 = e7.FullJoin(_sut.SimpleEntity, (p, t) => p.Item2.Id == t.Id);
        var t8 = t7.FullJoin<SimpleEntity>((p, t) => p.Item2.Id == t.Id);
        await AssertSameIds(e8, t8, p => p.Item1.Id);

    }

    [Fact]
    public void TestTypeOnlyJoin_NullPredicate_ThrowsBeforeSourceResolution_OnEveryReceiverAndKind()
    {
        var b1 = _sut.SimpleEntity;
        Assert.Throws<ArgumentNullException>(() => b1.Join<SimpleEntity>(null!));
        Assert.Throws<ArgumentNullException>(() => b1.LeftJoin<SimpleEntity>(null!));
        Assert.Throws<ArgumentNullException>(() => b1.RightJoin<SimpleEntity>(null!));
        Assert.Throws<ArgumentNullException>(() => b1.FullJoin<SimpleEntity>(null!));

        var b2 = b1.Join(_sut.SimpleEntity, (a, b) => a.Id == b.Id);
        Assert.Throws<ArgumentNullException>(() => b2.Join<SimpleEntity>(null!));
        Assert.Throws<ArgumentNullException>(() => b2.LeftJoin<SimpleEntity>(null!));
        Assert.Throws<ArgumentNullException>(() => b2.RightJoin<SimpleEntity>(null!));
        Assert.Throws<ArgumentNullException>(() => b2.FullJoin<SimpleEntity>(null!));

        var b3 = b2.Join(_sut.SimpleEntity, (p, n) => p.Item2.Id == n.Id);
        Assert.Throws<ArgumentNullException>(() => b3.Join<SimpleEntity>(null!));
        Assert.Throws<ArgumentNullException>(() => b3.LeftJoin<SimpleEntity>(null!));
        Assert.Throws<ArgumentNullException>(() => b3.RightJoin<SimpleEntity>(null!));
        Assert.Throws<ArgumentNullException>(() => b3.FullJoin<SimpleEntity>(null!));

        var b4 = b3.Join(_sut.SimpleEntity, (p, n) => p.Item2.Id == n.Id);
        Assert.Throws<ArgumentNullException>(() => b4.Join<SimpleEntity>(null!));
        Assert.Throws<ArgumentNullException>(() => b4.LeftJoin<SimpleEntity>(null!));
        Assert.Throws<ArgumentNullException>(() => b4.RightJoin<SimpleEntity>(null!));
        Assert.Throws<ArgumentNullException>(() => b4.FullJoin<SimpleEntity>(null!));

        var b5 = b4.Join(_sut.SimpleEntity, (p, n) => p.Item2.Id == n.Id);
        Assert.Throws<ArgumentNullException>(() => b5.Join<SimpleEntity>(null!));
        Assert.Throws<ArgumentNullException>(() => b5.LeftJoin<SimpleEntity>(null!));
        Assert.Throws<ArgumentNullException>(() => b5.RightJoin<SimpleEntity>(null!));
        Assert.Throws<ArgumentNullException>(() => b5.FullJoin<SimpleEntity>(null!));

        var b6 = b5.Join(_sut.SimpleEntity, (p, n) => p.Item2.Id == n.Id);
        Assert.Throws<ArgumentNullException>(() => b6.Join<SimpleEntity>(null!));
        Assert.Throws<ArgumentNullException>(() => b6.LeftJoin<SimpleEntity>(null!));
        Assert.Throws<ArgumentNullException>(() => b6.RightJoin<SimpleEntity>(null!));
        Assert.Throws<ArgumentNullException>(() => b6.FullJoin<SimpleEntity>(null!));

        var b7 = b6.Join(_sut.SimpleEntity, (p, n) => p.Item2.Id == n.Id);
        Assert.Throws<ArgumentNullException>(() => b7.Join<SimpleEntity>(null!));
        Assert.Throws<ArgumentNullException>(() => b7.LeftJoin<SimpleEntity>(null!));
        Assert.Throws<ArgumentNullException>(() => b7.RightJoin<SimpleEntity>(null!));
        Assert.Throws<ArgumentNullException>(() => b7.FullJoin<SimpleEntity>(null!));
    }

    [Fact]
    public async Task TestTypeOnlyJoin_Options_OmittedNullAndCallback()
    {
        _sut.SimpleEntity.WithData([new SimpleEntity { Id = 1 }, new SimpleEntity { Id = 2 }]);

        var omitted = _sut.SimpleEntity.Join<SimpleEntity>((t1, t2) => t1.Id == t2.Id + 1);
        var explicitNull = _sut.SimpleEntity.Join<SimpleEntity>((t1, t2) => t1.Id == t2.Id + 1, null);
        var oracle = _sut.SimpleEntity.Join(_sut.SimpleEntity, (t1, t2) => t1.Id == t2.Id + 1);

        (await IdsAsync(omitted, p => p.Item1.Id)).Should().Equal(await IdsAsync(oracle, p => p.Item1.Id));
        (await IdsAsync(explicitNull, p => p.Item1.Id)).Should().Equal(await IdsAsync(oracle, p => p.Item1.Id));

        var calls = 0;
        var withOptions = _sut.SimpleEntity.Join<SimpleEntity>((t1, t2) => t1.Id == t2.Id + 1, _ => calls++);
        calls.Should().Be(1, "the type-only overload must invoke the options callback once, like the explicit oracle");
        (await IdsAsync(withOptions, p => p.Item1.Id)).Should().Equal(await IdsAsync(oracle, p => p.Item1.Id));
    }

    [Fact]
    public void TestTypeOnlyJoin_Options_SentinelException_NotSwallowed()
    {
        var sentinel = new InvalidOperationException("t222-sentinel-options");

        var act = () => _sut.SimpleEntity.Join<SimpleEntity>((t1, t2) => t1.Id == t2.Id + 1, _ => throw sentinel);

        act.Should().Throw<InvalidOperationException>().WithMessage("t222-sentinel-options");
    }

    [Fact]
    public async Task TestTypeOnlyOuterJoins_UnmatchedSides_AreNull()
    {
        _sut.SimpleEntity.WithData([new SimpleEntity { Id = 1 }, new SimpleEntity { Id = 2 }]);

        var left = _sut.SimpleEntity.LeftJoin<SimpleEntity>((t1, t2) => t1.Id == t2.Id + 1)
            .Select(p => new { FirstId = p.Item1.Id, Right = p.Item2 });
        var leftRows = new List<(int FirstId, SimpleEntity? Right)>();
        await foreach (var r in left.ToAsyncEnumerable())
            leftRows.Add((r.FirstId, r.Right));
        leftRows.Should().HaveCount(2);
        leftRows.Count(r => r.Right is null).Should().Be(1);

        var right = _sut.SimpleEntity.RightJoin<SimpleEntity>((t1, t2) => t1.Id == t2.Id + 1)
            .Select(p => new { Left = p.Item1, RightId = p.Item2.Id });
        var rightRows = new List<(SimpleEntity? Left, int RightId)>();
        await foreach (var r in right.ToAsyncEnumerable())
            rightRows.Add((r.Left, r.RightId));
        rightRows.Should().HaveCount(2);
        rightRows.Count(r => r.Left is null).Should().Be(1);

        var full = _sut.SimpleEntity.FullJoin<SimpleEntity>((t1, t2) => t1.Id == t2.Id + 1)
            .Select(p => new { Left = p.Item1, Right = p.Item2 });
        var fullRows = new List<(SimpleEntity? Left, SimpleEntity? Right)>();
        await foreach (var r in full.ToAsyncEnumerable())
            fullRows.Add((r.Left, r.Right));
        fullRows.Should().HaveCount(3);
        fullRows.Count(r => r.Left is null || r.Right is null).Should().Be(2);
    }
}
