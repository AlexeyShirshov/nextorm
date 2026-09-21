using FluentAssertions;

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
            .Join(_sut.SimpleEntity, (t1, t2) => t1.Id == t2.Id)
            .WithStrictness(JoinStrictness.Any)
            .Select(p => new { FirstId = p.Item1.Id })
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*join modifier is not supported*");
    }

    [Fact]
    public void TestGlobalJoin_ShouldThrow()
    {
        var act = () => _sut.SimpleEntity
            .Join(_sut.SimpleEntity, (t1, t2) => t1.Id == t2.Id)
            .Global()
            .Select(p => new { FirstId = p.Item1.Id })
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*GLOBAL join modifier is not supported*");
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
}