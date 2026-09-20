using FluentAssertions;

namespace NextORM.Core.Tests;

/// <summary>
/// The in-memory provider cannot bind an outer row while executing a correlated subquery, so
/// correlated subqueries are rejected explicitly instead of silently ignoring the correlation
/// (which used to let every row through).
/// </summary>
public class CorrelatedQueryInMemoryTests
{
    private readonly InMemoryRepository _sut;

    public CorrelatedQueryInMemoryTests(InMemoryRepository sut)
    {
        _sut = sut;
        _sut.SimpleEntity.WithData(new[] { new SimpleEntity { Id = 1 }, new SimpleEntity { Id = 2 } });
    }

    [Fact]
    public void CorrelatedExists_ShouldThrowNotSupported()
    {
        var act = () => _sut.SimpleEntity
            .Where(it => SqlFunctions.Sql.exists(_sut.SimpleEntity.Where(s => s.Id == it.Id)))
            .Select(it => new { it.Id })
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*Correlated subqueries are not supported*");
    }

    [Fact]
    public void CorrelatedScalarInProjection_ShouldThrowNotSupported()
    {
        var act = () => _sut.SimpleEntity
            .Select(it => new { it.Id, sid = _sut.SimpleEntity.Where(s => s.Id == it.Id).Select(s => s.Id).First() })
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*Correlated subqueries are not supported*");
    }
}
