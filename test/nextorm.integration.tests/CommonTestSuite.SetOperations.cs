using FluentAssertions;

namespace nextorm.integration.tests;

public abstract partial class CommonTestSuite
{
    [Fact]
    public void Intersect_ShouldReturnOnlyCommonRows()
    {
        // simple_entity ids are 1..10, complex_entity ids are 1..3, so only 1, 2 and 3 are common.
        var cmd = _sut.SimpleEntity.Select(it => it.Id)
            .Intersect(_sut.ComplexEntity.Select(it => (int)it.Id));

        _sut.From(cmd).Count().Should().Be(3);
    }

    [Fact]
    public void Except_ShouldReturnOnlyLeftRows()
    {
        // The left side is 1..10; removing the right side leaves 4..10.
        var cmd = _sut.SimpleEntity.Select(it => it.Id)
            .Except(_sut.ComplexEntity.Select(it => (int)it.Id));

        _sut.From(cmd).Count().Should().Be(7);
    }

    [Fact]
    public void SetOperations_WhenChained_ShouldApplyLeftToRight()
    {
        // (simple EXCEPT complex) INTERSECT simple = {4..10} INTERSECT {1..10} = {4..10}.
        var cmd = _sut.SimpleEntity.Select(it => it.Id)
            .Except(_sut.ComplexEntity.Select(it => (int)it.Id))
            .Intersect(_sut.SimpleEntity.Select(it => it.Id));

        _sut.From(cmd).Count().Should().Be(7);
    }

    [Fact]
    public void IntersectAll_WhenSupported_ShouldReturnCommonRows()
    {
        Assert.SkipUnless(Provider.SupportsIntersectExceptAll,
            "This provider does not support INTERSECT ALL.");

        var cmd = _sut.SimpleEntity.Select(it => it.Id)
            .IntersectAll(_sut.ComplexEntity.Select(it => (int)it.Id));

        _sut.From(cmd).Count().Should().Be(3);
    }

    [Fact]
    public void ExceptAll_WhenSupported_ShouldReturnOnlyLeftRows()
    {
        Assert.SkipUnless(Provider.SupportsIntersectExceptAll,
            "This provider does not support EXCEPT ALL.");

        var cmd = _sut.SimpleEntity.Select(it => it.Id)
            .ExceptAll(_sut.ComplexEntity.Select(it => (int)it.Id));

        _sut.From(cmd).Count().Should().Be(7);
    }

    [Fact]
    public void IntersectAll_WhenUnsupported_ShouldThrow()
    {
        Assert.SkipUnless(!Provider.SupportsIntersectExceptAll,
            "This provider supports INTERSECT ALL.");

        var cmd = _sut.SimpleEntity.Select(it => it.Id)
            .IntersectAll(_sut.ComplexEntity.Select(it => (int)it.Id));

        var act = () => _sut.From(cmd).Count();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void ExceptAll_WhenUnsupported_ShouldThrow()
    {
        Assert.SkipUnless(!Provider.SupportsIntersectExceptAll,
            "This provider supports EXCEPT ALL.");

        var cmd = _sut.SimpleEntity.Select(it => it.Id)
            .ExceptAll(_sut.ComplexEntity.Select(it => (int)it.Id));

        var act = () => _sut.From(cmd).Count();

        act.Should().Throw<NotSupportedException>();
    }
}
