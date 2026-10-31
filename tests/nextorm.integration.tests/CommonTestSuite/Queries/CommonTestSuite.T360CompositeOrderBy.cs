using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T360 (issue #360): a composite <c>OrderBy</c>/<c>ThenBy</c> selector expands into one key per
/// member in declaration order, each carrying the call's direction and <c>NullsPosition</c>. The live
/// proof is made on the returned rows: the composite form must produce exactly the same sequence as
/// the equivalent chained <c>OrderBy(...).ThenBy(...)</c> form on every provider.
/// </summary>
public abstract partial class CommonTestSuite
{
    [Fact]
    public void T360_CompositeOrderBy_Anonymous_LiveEqualsChained()
    {
        _sut.ComplexEntity
            .OrderBy(x => new { x.Int, x.Id })
            .Select(x => x.Id)
            .ToList()
            .Should().Equal(_sut.ComplexEntity
                .OrderBy(x => x.Int)
                .ThenBy(x => x.Id)
                .Select(x => x.Id)
                .ToList());
    }

    [Fact]
    public void T360_CompositeOrderBy_Tuple_LiveEqualsChained()
    {
        _sut.ComplexEntity
            .OrderBy(x => new ValueTuple<int?, long>(x.Int, x.Id))
            .Select(x => x.Id)
            .ToList()
            .Should().Equal(_sut.ComplexEntity
                .OrderBy(x => x.Int)
                .ThenBy(x => x.Id)
                .Select(x => x.Id)
                .ToList());
    }

    [Fact]
    public void T360_CompositeOrderBy_MemberInit_LiveEqualsChained()
    {
        _sut.ComplexEntity
            .OrderBy(x => new T360CompositeOrderByKey { Int = x.Int, Id = x.Id })
            .Select(x => x.Id)
            .ToList()
            .Should().Equal(_sut.ComplexEntity
                .OrderBy(x => x.Int)
                .ThenBy(x => x.Id)
                .Select(x => x.Id)
                .ToList());
    }

    [Fact]
    public void T360_CompositeOrderByDescending_LiveEqualsChained()
    {
        _sut.ComplexEntity
            .OrderByDescending(x => new { x.Int, x.Id })
            .Select(x => x.Id)
            .ToList()
            .Should().Equal(_sut.ComplexEntity
                .OrderByDescending(x => x.Int)
                .ThenByDescending(x => x.Id)
                .Select(x => x.Id)
                .ToList());
    }

    [Fact]
    public void T360_CompositeThenBy_LiveEqualsChained()
    {
        _sut.ComplexEntity
            .OrderBy(x => x.Id)
            .ThenBy(x => new { x.Int })
            .Select(x => x.Id)
            .ToList()
            .Should().Equal(_sut.ComplexEntity
                .OrderBy(x => x.Id)
                .ThenBy(x => x.Int)
                .Select(x => x.Id)
                .ToList());
    }

    /// <summary>
    /// A composite primary key followed by a composite <c>ThenByDescending</c> secondary key. The seeded
    /// rows 2 and 3 share <c>nullableint = 1</c> and <c>tinyval = 2</c>, so only the descending secondary
    /// key can break the tie: <c>id</c> descending puts 3 before 2 on every provider.
    /// </summary>
    [Fact]
    public void T360_CompositeThenByDescending_MultiKeyTieBreak_LiveEqualsChained()
    {
        _sut.ComplexEntity
            .Where(x => x.Int != null)
            .OrderBy(x => new { x.Int, x.TinyInt })
            .ThenByDescending(x => new { x.Id, x.Boolean })
            .Select(x => x.Id)
            .ToList()
            .Should().Equal(3L, 2L);

        _sut.ComplexEntity
            .OrderBy(x => new { x.Int, x.TinyInt })
            .ThenByDescending(x => new { x.Id, x.Boolean })
            .Select(x => x.Id)
            .ToList()
            .Should().Equal(_sut.ComplexEntity
                .OrderBy(x => x.Int)
                .ThenBy(x => x.TinyInt)
                .ThenByDescending(x => x.Id)
                .ThenByDescending(x => x.Boolean)
                .Select(x => x.Id)
                .ToList());
    }

    [Fact]
    public void T360_CompositeOrderBy_LiveEqualsChained()
    {
        // Anonymous composite, ascending.
        _sut.ComplexEntity
            .OrderBy(x => new { x.Int, x.Id })
            .Select(x => x.Id)
            .ToList()
            .Should().Equal(_sut.ComplexEntity
                .OrderBy(x => x.Int)
                .ThenBy(x => x.Id)
                .Select(x => x.Id)
                .ToList());

        // Tuple composite, descending (direction propagates to every key).
        _sut.ComplexEntity
            .OrderBy(x => new ValueTuple<int?, long>(x.Int, x.Id), OrderDirection.Desc)
            .Select(x => x.Id)
            .ToList()
            .Should().Equal(_sut.ComplexEntity
                .OrderBy(x => x.Int, OrderDirection.Desc)
                .ThenBy(x => x.Id, OrderDirection.Desc)
                .Select(x => x.Id)
                .ToList());

        // Member initializer, with an explicit NullsPosition.
        _sut.ComplexEntity
            .OrderBy(x => new T360CompositeOrderByKey { Int = x.Int, Id = x.Id }, OrderDirection.Asc, NullsPosition.Last)
            .Select(x => x.Id)
            .ToList()
            .Should().Equal(_sut.ComplexEntity
                .OrderBy(x => x.Int, OrderDirection.Asc, NullsPosition.Last)
                .ThenBy(x => x.Id, OrderDirection.Asc, NullsPosition.Last)
                .Select(x => x.Id)
                .ToList());
    }
}

/// <summary>Projection DTO used by the member-initializer composite case.</summary>
internal sealed class T360CompositeOrderByKey
{
    public int? Int { get; set; }
    public long Id { get; set; }
}
